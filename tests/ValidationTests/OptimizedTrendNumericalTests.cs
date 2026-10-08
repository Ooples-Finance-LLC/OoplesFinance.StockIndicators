using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class OptimizedTrendNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("OTT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(OptimizedTrendTracker)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStopSegments(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.OptimizedTrendOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.VariableIndexDynamicAverage, MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.VariableIndexDynamicAverage ? 19 : kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.VariableIndexDynamicAverage, double percent = 1.4)
    {
        var expected = BuiltInFormulaReferences.OptimizedTrendValues(bars, length, Kind(kind), percent); var data = Data(bars).CalculateOptimizedTrendTracker(kind, length, percent);
        Assert.Equal(expected.Values, data.OutputValues["Ott"]); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeOptimizedTrendTrackerFast(Data(bars), context, length, percent, kind); Assert.Equal(expected.Values, fast.ToArray());
        using var state = new OptimizedTrendTrackerState(kind, length, percent);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Ott"]); } }
        }
        return expected.Values;
    }
    [Fact]
    public void WideAveragesAndPercentagesKeepEveryStopAndDirection()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var percent in new[] { -200d, -2, 0, 1.4, 50, 200, 1000 })
            Check(Bars(new[] { 2d, 4, 6, 4, 2, 4, 6, -4, -6, -2, 3, 3, 5, -3, 0, 0 }.Select(p => p * scale)), length, kind, percent);
    }
    [Fact]
    public void HandStopsPreserveStrictCrossingsEqualityAndRatchets()
    {
        Assert.Equal(new[] { 62.5, 31.25 }, Check(Bars(new[] { 100d, 50 }), 1, MovingAvgType.SimpleMovingAverage, 50));
        Assert.Equal(new[] { 62.5, 45, 67.5 }, Check(Bars(new[] { 100d, 40, 60 }), 1, MovingAvgType.SimpleMovingAverage, 50));
        var actual = Check(Bars(new[] { 100d, 105, 103, 94, 90, 94, 105 }), 1, MovingAvgType.SimpleMovingAverage, 10); var expected = new[] { 94.5, 99.225, 99.225, 98.23, 94.05, 94.05, 99.225 }; for (var i = 0; i < actual.Length; i++) Assert.Equal(expected[i], actual[i], 12);
        Check(Bars(new[] { -100d, -50, -40, -60, -100, -105, -94 }), 1, MovingAvgType.SimpleMovingAverage, 50);
    }
    [Fact]
    public void VidyaKeepsMomentumExpiryAndFirstPriceSeed()
    {
        Assert.Equal(new[] { 2d, 3, 3, 4.5 }, Check(Bars(new[] { 2d, 4, 2, 8 }), 3, percent: 0));
        Assert.Equal(new[] { 7d, 7, 7, 7 }, Check(Bars(new[] { 7d, 7, 7, 7 }), 3, percent: 0));
        Check(Bars(new[] { 0d, double.MaxValue, 0, 0, 0, 0, 0, 1, 2, 1, 3, 0 }), 3, percent: 0);
    }
    [Fact]
    public void ExtendedStopsCancelBeforeProjectionAndRecover()
    {
        var max = double.MaxValue; Assert.Equal(new[] { double.PositiveInfinity, 0d }, Check(Bars(new[] { max, -max }), 1, MovingAvgType.SimpleMovingAverage, -200));
        var output = Check(Bars(new[] { max, -max, 0d, 1, -1, 0, 0 }), 1, MovingAvgType.SimpleMovingAverage, max); Assert.Equal(0d, output[^1]); Assert.DoesNotContain(output, double.IsNaN);
        Check(Bars(new[] { double.Epsilon, 0d, 2 * double.Epsilon, 0 }), 1, percent: max);
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepVidyaAndCoreMeansLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds) { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, kind); }
    }
    [Fact]
    public void CustomAndLegacyAveragesReceiveSelectedPricesAndPeriod()
    {
        var bars = Bars(new[] { 2d, 4, 6 }); var prices = new[] { 7d, 8, 9 }; var averages = new[] { 100d, 40, 60 }; var expected = BuiltInFormulaReferences.OptimizedTrendValues(Bars(prices), 2, 1, 50, averages);
        foreach (var fast in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(2, period); Assert.Equal(prices, values); return averages; }); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(prices.ToList());
            if (fast) { using var output = IndicatorCompute.ComputeOptimizedTrendTrackerFast(data, context, 2, 50); Assert.Equal(expected.Values, output.ToArray()); } else { data.CalculateOptimizedTrendTracker(length: 2, percent: 50); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); } Assert.Equal(1, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var mean = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, bars.Select(b => b.Close).ToList()).ToArray(); var reference = BuiltInFormulaReferences.OptimizedTrendValues(bars, 2, 1, 50, mean); var batch = Data(bars).CalculateOptimizedTrendTracker(kind, 2, 50); Assert.Equal(reference.Values, batch.CustomValuesList); Assert.Equal(reference.Signals, batch.SignalsList); using var ctx = new ComputeContext(); using var result = IndicatorCompute.ComputeOptimizedTrendTrackerFast(Data(bars), ctx, 2, 50, kind); Assert.Equal(reference.Values, result.ToArray());
        var selectedData = Data(bars); selectedData.SetCustomValues(prices.ToList()); var selectedExpected = BuiltInFormulaReferences.OptimizedTrendValues(Bars(prices), 2, 19, 50); using var selectedFast = IndicatorCompute.ComputeOptimizedTrendTrackerFast(selectedData, ctx, 2, 50); Assert.Equal(selectedExpected.Values, selectedFast.ToArray()); selectedData.CalculateOptimizedTrendTracker(length: 2, percent: 50); Assert.Equal(selectedExpected.Values, selectedData.CustomValuesList); Assert.Equal(selectedExpected.Signals, selectedData.SignalsList);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMeansStopsOrDirection()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new OptimizedTrendTrackerState(length: 2); using var control = new OptimizedTrendTrackerState(length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
    [Fact]
    public void NonfinitePercentagesRejectBeforeMutatingSelectedInput()
    {
        foreach (var percent in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        { var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateOptimizedTrendTracker(percent: percent)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new OptimizedTrendTrackerState(percent: percent)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeOptimizedTrendTrackerFast(data, context, percent: percent)); Assert.Equal(selected, data.CustomValuesList); }
    }
}
