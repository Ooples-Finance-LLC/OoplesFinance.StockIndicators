using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EnvelopeNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(MovingAverageEnvelope)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Contains(report.FixtureEvidence, f => f.Name == "moving-average-envelope-" + sign + "-output-overflow" && f.Completed && f.Passed);
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task AllStagesMatchTheIndependentReferenceAcrossRoutes(int length)
    {
        var averages = new (IMovingAverage Average, int Kind)[] {
            (new Sma(),1), (new Wma(),2), (new Ema(),3), (new Wwma(),6),
            (new SymmetricallyWeightedMovingAverage(),7), (new FibonacciWeightedMovingAverage(),8),
            (new SquareRootWeightedMovingAverage(),9), (new ParabolicWma(),10), (new CubedWeightedMovingAverage(),11),
            (new QuickMovingAverage(),12), (new JsaMovingAverage(),13), (new QuadraticMovingAverage(),14),
            (new Kama(),15), (new SineWma(),16), (new NaturalMa(),17), (new EhlersHannMovingAverage(),18), (new Vidya(),19) };
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            Fixture("bands-overflow-middle-finite", Enumerable.Repeat(double.MaxValue, 32).ToArray()),
            Fixture("band-small-contribution", new[] { 1e100, 1e100, 1e100, double.Epsilon, double.Epsilon }) }))
        foreach (var (average, kind) in averages)
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedEnvelope(bars, length, kind, .025);
            IIndicator indicator = new MovingAverageEnvelope(length, .025, average);
            var builtIn = (IBuiltInIndicator)indicator;
            var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "MiddleBand" } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            if (keys.Any(key => expected[key].Any(double.IsInfinity)))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                for (var slot = 0; slot < keys.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
            }
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            data.CalculateMovingAverageEnvelope(maType, length, .025);
            foreach (var (key, values) in expected) Assert.Equal(values, data.OutputValues[key]);
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
                        foreach (var (key, values) in expected) Assert.Equal(values[i], result.Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedPricesArePreserved()
    {
        var bars = Fixture("selection", new[] { 100d, 120, 110, 150, 90 }).Bars;
        var source = new Sma(2);
        IIndicator[] indicators = { new MovingAverageEnvelope(3).Of(source) };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators.Prepend(source).ToArray()).BuildAsync();
        var expected = BuiltInFormulaReferences.RoundedEnvelope(Fixture("selected", run[source].ToArray()).Bars, 3, 1, .025);
        foreach (var indicator in indicators)
        {
            var builtIn = (IBuiltInIndicator)indicator;
            var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "MiddleBand" } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            for (var slot = 0; slot < keys.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
        }
    }

    [Fact]
    public void ExtremeMultipliersRetainFiniteLowerBandsDespiteOverflowingProducts()
    {
        var bars = Fixture("percentages", new[] { double.MaxValue, double.MaxValue / 2, 1e100, -1e100, double.Epsilon }).Bars;
        foreach (var fraction in new[] { 0d, 1d, Math.BitIncrement(1d), -1d, 1e-16, double.MaxValue })
        {
            var expected = BuiltInFormulaReferences.RoundedEnvelope(bars, 1, 1, fraction);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateMovingAverageEnvelope(length: 1, mult: fraction);
            foreach (var (key, values) in expected) Assert.Equal(values, data.OutputValues[key]);
            using var state = new MovingAverageEnvelopeState(length: 1, mult: fraction);
            for (var i = 0; i < bars.Count; i++)
            {
                var b = bars[i];
                var bar = new OhlcvBar("ENVELOPE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                var actual = state.Update(bar, true, true);
                foreach (var (key, values) in expected) Assert.Equal(values[i], actual.Outputs![key]);
            }
        }
    }

    private static IndicatorValidationFixture Fixture(string name, double[] values) => new(name,
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)));
}
