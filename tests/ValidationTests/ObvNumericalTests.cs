using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ObvNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Obv) || c.IndicatorType == typeof(OnBalanceVolume)).Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
        => Assert.Equal(114, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("obv-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == "obv-" + sign + "-output-overflow").OutputOverflowRejectionsChecked);
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(14, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(14, true)]
    public async Task AllStagesMatchTheIndependentReferenceAcrossRoutes(int length, bool expanded)
    {
        var averages = new (IMovingAverage Average, int Kind)[] {
            (new Sma(),1), (new Wma(),2), (new Ema(),3), (new Wwma(),6),
            (new SymmetricallyWeightedMovingAverage(),7), (new FibonacciWeightedMovingAverage(),8),
            (new SquareRootWeightedMovingAverage(),9), (new ParabolicWma(),10), (new CubedWeightedMovingAverage(),11),
            (new QuickMovingAverage(),12), (new JsaMovingAverage(),13), (new QuadraticMovingAverage(),14),
            (new Kama(),15), (new SineWma(),16), (new NaturalMa(),17), (new EhlersHannMovingAverage(),18), (new Vidya(),19) };
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            new IndicatorValidationFixture("small-cumulative-contribution", new[] { 1e100, 1d, -1e100, 2 }
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 1, i + 1, i + 1, v))) }))
        foreach (var (average, kind) in averages)
        {
            var bars = fixture.Bars;
            var raw = BuiltInFormulaReferences.RoundedObv(bars);
            var count = Array.FindIndex(raw, double.IsInfinity);
            if (count >= 0) bars = bars.Take(count).ToArray();
            var expected = new Dictionary<string, double[]> {
                ["Obv"] = BuiltInFormulaReferences.RoundedObv(bars),
                ["ObvSignal"] = BuiltInFormulaReferences.RoundedObvSignal(bars, length, kind) };
            IIndicator indicator = expanded ? new OnBalanceVolume(length, average) : new Obv(length, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected["Obv"], run[indicator].ToArray());
            Assert.Equal(expected["ObvSignal"], run[indicator.Outputs[1]].ToArray());
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            data.CalculateOnBalanceVolume(maType, length);
            Assert.Equal(expected["Obv"], data.OutputValues["Obv"]);
            Assert.Equal(expected["ObvSignal"], data.OutputValues["ObvSignal"]);
            var builtIn = (IBuiltInIndicator)indicator;
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("FAST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        var result = state.Update(native, commit, true);
                        Assert.Equal(expected["Obv"][i], result.Outputs!["Obv"]);
                        Assert.Equal(expected["ObvSignal"][i], result.Outputs["ObvSignal"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task TypedChainingAndBothBuildersUseSelectedPricesAndUpdatedVolume()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, i + 1)).ToArray();
        var source = new Sma(2);
        foreach (var indicator in new IIndicator[] { new Obv(3, new Sma()), new OnBalanceVolume(3, new Sma()) })
        {
            ((MultiOutputIndicatorBase)indicator).Of(source);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(source, indicator).BuildAsync();
            var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, v, v, v, v, bars[i].Volume)).ToArray();
            Assert.Equal(BuiltInFormulaReferences.RoundedObv(projected), run[indicator].ToArray());
            Assert.Equal(BuiltInFormulaReferences.RoundedObvSignal(projected, 3, 1), run[indicator.Outputs[1]].ToArray());
        }
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        _ = data.TickerDataList;
        data.InputValues = new() { 1, 1, 0, 2 };
        data.Volumes = new() { 3, 3, 3, 3 };
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var compact = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeObvFast(data, context, 3);
        using var expanded = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeOnBalanceVolumeFast(data, context);
        Assert.Equal(new[] { 3d, 3, 0, 3 }, compact.ToArray());
        Assert.Equal(new[] { 3d, 3, 0, 3 }, expanded.ToArray());
    }

    [Fact]
    public void ModifiedConsumerRetainsTheSmallCumulativeContribution()
    {
        var volumes = new[] { 1e100, 1d, -1e100, 2, 0, 0 };
        using var state = new OnBalanceVolumeModifiedState(MovingAvgType.SimpleMovingAverage, 1, 1);
        var bars = volumes.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, i + 1, i + 1, i + 1, v)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedObv(bars);
        Assert.Equal(1, expected[2]);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            foreach (var commit in new[] { false, true })
            {
                var b = bars[i];
                var native = new OhlcvBar("OBV", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                var result = state.Update(native, commit, true);
                Assert.Equal(expected[i], result.Outputs!["Obvm"]);
                Assert.Equal(expected[i], result.Outputs["Signal"]);
            }
        }
    }
}
