using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ZeroCrossingCycleNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = -4) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar {i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersZeroCrossingsDominantCycle)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCrossingIntervals(IndicatorValidationCase c, string route)
    {
        var options = (EhlersZeroCrossingsDominantCycleSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ZeroCrossingCycleValues(bars, options.Length, options.Bw).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 20, double bandwidth = .7)
    {
        var expected = BuiltInFormulaReferences.ZeroCrossingCycleValues(bars, length, bandwidth); var values = expected.Outputs["Ezcdc"]; var batch = Data(bars).CalculateEhlersZeroCrossingsDominantCycle(length, bandwidth); Assert.Equal(new[] { "Ezcdc" }, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(values, batch.CustomValuesList); Equal(values, batch.OutputValues["Ezcdc"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersZeroCrossingsDominantCycleFast(Data(bars), context, length, bandwidth); Equal(values, fast.ToArray()); var state = new EhlersZeroCrossingsDominantCycleState(length, bandwidth);
        for (var replay = 0; replay < 2; replay++)
        {
            for (var i = 0; i < 40; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(values[i], actual.Value); Assert.Equal(values[i], actual.Outputs!["Ezcdc"]); } }
        }
        return values;
    }
    [Fact]
    public void WideAndSubnormalPricesRetainCyclesAndDirectionalSignals()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) foreach (var bandwidth in new[] { -.7, .01, .7, double.MaxValue })
        { var values = Check(Enumerable.Range(0, 85).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(), 7, bandwidth); Assert.All(values, value => Assert.True(double.IsFinite(value) && value >= 0)); }
    }
    [Fact]
    public void StartupFlatStreamsAndExtremePeriodsKeepTheSixBarFloor()
    {
        Check(Array.Empty<Bar>()); foreach (var value in new[] { 0d, double.MaxValue, -double.MaxValue, double.Epsilon }) Assert.All(Check(Enumerable.Repeat(Candle(value), 50).ToArray()), cycle => Assert.Equal(6, cycle));
        Equal(new[] { 6d, 6, 6, 7.5 }, Check(new[] { 0d, 1, 2, 3 }.Select(v => Candle(v)).ToArray()));
        foreach (var length in new[] { int.MinValue, 0, 1, 31, int.MaxValue }) Check(Enumerable.Range(0, 80).Select(i => Candle(Math.Sin(i * .3))).ToArray(), length);
    }
    [Fact]
    public void ReflectionAndPowerOfTwoScalingPreserveCrossingIntervals()
    {
        var prices = Enumerable.Range(0, 130).Select(i => Math.Sin(i * .25) + .25 * Math.Cos(i * .71)).ToArray(); var expected = Check(prices.Select(v => Candle(v)).ToArray(), 9);
        foreach (var scale in new[] { -1d, Math.Pow(2, -700), Math.Pow(2, 700) }) Equal(expected, Check(prices.Select(v => Candle(v * scale)).ToArray(), 9));
    }
    [Fact]
    public void SelectedPricesReachBatchAndFast()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.ZeroCrossingCycleValues(selected.Select(v => Candle(v)).ToArray(), 7);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersZeroCrossingsDominantCycle(7); Equal(expected.Outputs["Ezcdc"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersZeroCrossingsDominantCycleFast(source, context, 7); Equal(expected.Outputs["Ezcdc"], result.ToArray());
    }
    [Fact]
    public void LongIntervalsCannotWrapBeforeCycleClamping()
    {
        // Seed only elapsed time to exercise the long-running counter boundary without billions of bars.
        var window = new ZeroCrossingCycleWindow(20, .7); for (var i = 0; i < 3; i++) window.Next(i, true);
        typeof(ZeroCrossingCycleWindow).GetField("_counter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(window, (long)int.MaxValue);
        Assert.Equal(7.5, window.Next(3, false).Value); Assert.Equal(7.5, window.Next(3, true).Value);
    }
    [Fact]
    public void NonfiniteBandwidthIsRejectedBeforeProcessing()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        { Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersZeroCrossingsDominantCycle(bw: invalid)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersZeroCrossingsDominantCycleState(bw: invalid)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeEhlersZeroCrossingsDominantCycleFast(Data(Array.Empty<Bar>()), context, bw: invalid)); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceCrossingsOrBandPass()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = new EhlersZeroCrossingsDominantCycleState(); var control = new EhlersZeroCrossingsDominantCycleState();
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Ezcdc"], actual.Outputs!["Ezcdc"]); }
        }
    }
}
