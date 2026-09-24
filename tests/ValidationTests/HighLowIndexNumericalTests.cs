using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class HighLowIndexNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(HighLowIndex)).Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
        => Assert.Equal(57, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("high-low-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
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
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        foreach (var (average, kind) in averages)
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedHighLowIndex(bars, length, kind);
            var indicator = new HighLowIndex(length, average);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator].ToArray());
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            data.CalculateHighLowIndex(maType, length);
            Assert.Equal(expected, data.OutputValues["Zmbti"]);
            Assert.All(expected, v => Assert.InRange(v, 0, 100));
            if (kind == 3)
            {
                var core = new double[bars.Count];
                OoplesFinance.StockIndicators.Core.OscillatorCore.HighLowIndex(bars.Select(b => b.High).ToArray(),
                    bars.Select(b => b.Low).ToArray(), core, length);
                Assert.Equal(expected, core);
            }
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
                        Assert.Equal(expected[i], result.Outputs!["Zmbti"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task TypedChainingKeepsHighLowColumnsAndCurrentColumnsAreUsed()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 2, v - 3, v, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new HighLowIndex(3, new Sma()).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, indicator).BuildAsync();
        Assert.Equal(BuiltInFormulaReferences.RoundedHighLowIndex(bars, 3, 1), run[indicator].ToArray());
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        _ = data.TickerDataList;
        data.HighPrices = new() { 0, 0, 0, 0 };
        data.LowPrices = new() { 0, 0, 0, 0 };
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var result = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeHighLowIndexFast(data, context, 3);
        Assert.Equal(new double[4], result.ToArray());
    }
}
