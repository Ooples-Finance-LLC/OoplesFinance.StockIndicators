using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MedianAdaptiveNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select(p => new Bar(DateTime.UnixEpoch, p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersMedianAverageAdaptiveFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesExhaustiveMedianSearch(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MedianAdaptiveOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 9, double threshold = .002)
    {
        var expected = BuiltInFormulaReferences.MedianAdaptiveValues(bars, length, threshold); var batch = Data(bars).CalculateEhlersMedianAverageAdaptiveFilter(length, threshold);
        Assert.Equal(expected.Outputs["Maaf"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Maaf"], batch.OutputValues["Maaf"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersMedianAverageAdaptiveFilterFast(Data(bars), context, length, threshold); Assert.Equal(expected.Outputs["Maaf"], fast.ToArray());
        using var state = new EhlersMedianAverageAdaptiveFilterState(length, threshold); var window = new MedianAdaptiveWindow(length, threshold);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -99d })[0]), false, false); window.Next(-99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["Maaf"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Maaf"]); Assert.Equal(point.Value, direct.Value);
                    Assert.Equal(expected.Signals[i], direct.Trade); Assert.Equal(expected.Periods[i], direct.Period); Assert.Equal(expected.Candidates[i], direct.Candidate);
                }
            }
        }
        return expected.Outputs["Maaf"];
    }
    [Fact]
    public void HandFirAndFloorPeriodRetainZeroPrehistory()
    {
        var values = new[] { .5, 1.75, 3.375, 4.6875 };
        Assert.Equal(values, Check(Bars(new[] { 6d, 6, 6, 6 }), 1));
        Assert.Equal(values, Check(Bars(new[] { 6d, 6, 6, 6 }), 3, .2));
        Assert.Equal(values, Check(Bars(new[] { 6d, 6, 6, 6 }), 2, -1));
    }
    [Fact]
    public void ExhaustiveOracleCoversSearchDirectionParityAndThresholds()
    {
        var random = new Random(632);
        foreach (var length in new[] { 1, 2, 3, 6, 9, 30, 79 }) foreach (var threshold in new[] { -.1, 0, .002, .15, .2, .25 })
            Check(Bars(Enumerable.Range(0, 17).Select(_ => random.Next(-20, 21) / 8d)), length, threshold);
    }
    [Fact]
    public void ConvexStagesRemainFiniteAcrossExtremeAndTinyInputs()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -500), 1, double.MaxValue / 4 })
        {
            Check(Bars(Enumerable.Range(0, 23).Select(i => (i % 7 - 3) * scale)));
            Check(Bars(Enumerable.Repeat(scale, 23)), 8);
        }
        Check(Bars(new[] { double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0, 1, -1 }), 7);
    }
    [Fact]
    public void MaximumPeriodsDoNotAllocateOrIterateNominalHistory()
    {
        foreach (var length in new[] { int.MaxValue, int.MaxValue - 1 })
        {
            var window = new MedianAdaptiveWindow(length, .002); var first = window.Next(6, true);
            Assert.Equal(.5, first.Value); Assert.Equal(3, first.Period);
            using var state = new EhlersMedianAverageAdaptiveFilterState(length, .002); Assert.Equal(.5, state.Update(Native(Bars(new[] { 6d })[0]), true, true).Value);
            var batch = Data(Bars(new[] { 6d })).CalculateEhlersMedianAverageAdaptiveFilter(length); Assert.Equal(.5, batch.CustomValuesList[0]);
            using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersMedianAverageAdaptiveFilterFast(Data(Bars(new[] { 6d })), context, length); Assert.Equal(.5, fast.ToArray()[0]);
            Assert.Equal(0, new MedianAdaptiveWindow(length, .002).Next(0, true).Value);
        }
        Assert.Equal(Math.Pow(2, -30), new MedianAdaptiveWindow(int.MaxValue, .2).Next(6, true).Value);
    }
    [Fact]
    public void HistoryCompactionPreservesTrailingMedians()
        => Check(Bars(Enumerable.Range(0, 2100).Select(i => (double)(i % 13 - 6))), 3, .15);
    [Fact]
    public void SelectedInputDoesNotConsumeComponentOverrides()
    {
        var selected = Enumerable.Range(0, 17).Select(i => (double)(i % 7 - 3)).ToArray(); var original = Bars(Enumerable.Repeat(99d, selected.Length));
        var expected = BuiltInFormulaReferences.MedianAdaptiveValues(Bars(selected), 9, .002).Outputs["Maaf"]; var data = Data(original); data.SetCustomValues(selected.ToList()); var calls = 0;
        using var overrides = ComponentAverage.Arm((values, _) => { calls++; return values; });
        var batch = data.CalculateEhlersMedianAverageAdaptiveFilter(9); Assert.Equal(expected, batch.CustomValuesList);
        data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersMedianAverageAdaptiveFilterFast(data, context, 9); Assert.Equal(expected, fast.ToArray()); Assert.Equal(0, calls);
    }
    [Fact]
    public void InvalidParametersAndCandlesRejectBeforeStateMutation()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MedianAdaptiveWindow(9, bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new EhlersMedianAverageAdaptiveFilterState(9); using var control = new EhlersMedianAverageAdaptiveFilterState(9);
                var seed = Native(Bars(new[] { 3d })[0]); state.Update(seed, true, false); control.Update(seed, true, false);
                var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
                foreach (var b in Bars(new[] { 7d, -2, 0 })) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
            }
        }
    }
}
