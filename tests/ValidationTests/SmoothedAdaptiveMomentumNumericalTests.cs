using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SmoothedAdaptiveMomentumNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSmoothedAdaptiveMomentum)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = (EhlersSmoothedAdaptiveMomentumSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.SmoothedAdaptiveMomentumValues(bars, o.Length1, o.Length2, o.MaType); }
    private static Dictionary<string, double[]> Check(Bar[] bars, int first = 5, int second = 8, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.SmoothedAdaptiveMomentumValues(bars, first, second, kind); var batch = Data(bars).CalculateEhlersSmoothedAdaptiveMomentum(kind, first, second);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]); Assert.Equal(expected["Esam"], batch.CustomValuesList);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var fast = IndicatorCompute.ComputeEhlersSmoothedAdaptiveMomentumFast(Data(bars), context, first, second, kind, key == "Esam" ? IndicatorCompute.MacdSeries.Line : IndicatorCompute.MacdSeries.Signal); Assert.Equal(expected[key], fast.ToArray()); }
        using var state = new EhlersSmoothedAdaptiveMomentumIndicatorState(kind, first, second);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Esam"][i], point.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideAndTinyMomentumRetainsFilterAndSignalFeedback()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue })
            {
            var values = Check(Enumerable.Range(0, 150).Select(i => Candle(i < 70 ? 0 : i < 95 ? scale : i < 125 ? -scale : scale)).ToArray(), 5, 8, kind);
            Assert.Contains(values["Esam"], v => v != 0); Assert.Contains(values["Signal"], v => v != 0);
        }
        Check(new[] { double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon, double.Epsilon }.Select(v => Candle(v)).ToArray());
    }
    [Fact]
    public void ThreePoleStartsAtZeroAndMeasuresTheAdaptiveLag()
    {
        var values = Check(Enumerable.Range(0, 90).Select(i => Candle(i + 1)).ToArray()); Assert.Equal(0, values["Esam"][0]); Assert.Contains(values["Esam"], v => v != 0);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage }) Check(Enumerable.Range(0, 90).Select(i => Candle(i % 9 < 4 ? 4 : -3)).ToArray(), 1, 2, kind);
    }
    [Fact]
    public void SignalAveragesRetainSubnormalIntermediateValues()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage })
        {
            var result = Check(Enumerable.Range(0, 150).Select(i => Candle(i < 70 ? 0 : i < 110 ? 8 * double.Epsilon : 0)).ToArray(), 5, 8, kind);
            Assert.Contains(result["Signal"], v => v != 0);
        }
    }
    [Fact]
    public void ExtremePeriodsUseConsumedHistoryAndPreserveSmallGain()
    {
        var bars = Enumerable.Range(0, 100).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage }) foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue })
        { Check(bars, length, length, kind); Check(Array.Empty<Bar>(), length, length, kind); }
        var result = Check(Enumerable.Range(0, 100).Select(i => Candle(i)).ToArray(), 5, int.MaxValue); Assert.Contains(result["Esam"], v => v != 0);
    }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastPaths()
    {
        var selected = Enumerable.Range(0, 70).Select(i => (double)(i % 7 - 3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.SmoothedAdaptiveMomentumValues(selected.Select(v => Candle(v)).ToArray(), 3, 8, MovingAvgType.ExponentialMovingAverage);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersSmoothedAdaptiveMomentum(length1: 3); foreach (var key in expected.Keys) Assert.Equal(expected[key], data.OutputValues[key]);
        using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected);
        foreach (var key in expected.Keys) { using var fast = IndicatorCompute.ComputeEhlersSmoothedAdaptiveMomentumFast(data, context, 3, series: key == "Esam" ? IndicatorCompute.MacdSeries.Line : IndicatorCompute.MacdSeries.Signal); Assert.Equal(expected[key], fast.ToArray()); }
    }
    [Fact]
    public void InvalidFieldsLeavePeriodFilterAndSignalUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersSmoothedAdaptiveMomentumIndicatorState(); using var control = new EhlersSmoothedAdaptiveMomentumIndicatorState(); state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 25).Select(i => Native(Candle(i % 3 - 1)))) { var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
    [Fact]
    public void ComponentOverrideConsumesTheFilteredLine()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray(); var expected = BuiltInFormulaReferences.SmoothedAdaptiveMomentumValues(bars, 5, 8, MovingAvgType.ExponentialMovingAverage)["Esam"];
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; using var armed = ComponentAverage.Arm((values, period) => { calls++; Assert.Equal(8, period); Assert.Equal(expected, values); return Enumerable.Repeat(17d, values.Count).ToArray(); });
            if (fast) { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersSmoothedAdaptiveMomentumFast(Data(bars), context, series: IndicatorCompute.MacdSeries.Signal); Assert.All(result.ToArray(), v => Assert.Equal(17, v)); }
            else { var result = Data(bars).CalculateEhlersSmoothedAdaptiveMomentum(); Assert.All(result.OutputValues["Signal"], v => Assert.Equal(17, v)); Assert.Equal(expected, result.CustomValuesList); }
            Assert.Equal(1, calls);
        }
    }
    [Fact]
    public void OtherAverageKindsKeepTheirExistingSignalSmoother()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray(); var batch = Data(bars).CalculateEhlersSmoothedAdaptiveMomentum(MovingAvgType.WildersSmoothingMethod);
        using var state = new EhlersSmoothedAdaptiveMomentumIndicatorState(MovingAvgType.WildersSmoothingMethod); using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeEhlersSmoothedAdaptiveMomentumFast(Data(bars), context, maType: MovingAvgType.WildersSmoothingMethod, series: IndicatorCompute.MacdSeries.Signal);
        Assert.Equal(batch.OutputValues["Signal"], fast.ToArray());
        for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); Assert.Equal(batch.CustomValuesList[i], point.Value); Assert.Equal(batch.OutputValues["Signal"][i], point.Outputs!["Signal"]); }
    }
}
