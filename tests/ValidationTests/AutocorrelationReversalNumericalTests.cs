using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AutocorrelationReversalNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = -4) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar {i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAutoCorrelationReversals)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCrossings(IndicatorValidationCase c, string route)
    {
        var options = (EhlersAutoCorrelationReversalsSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AutocorrelationReversalValues(bars, options.Length1, options.Length2, options.Length3, Kind(options.MaType)).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Bar[] Noisy(int count = 85)
    { var random = new Random(578); return Enumerable.Range(0, count).Select(_ => Candle(random.NextDouble() * 2 - 1)).ToArray(); }
    private static double[] Check(Bar[] bars, int length = 2, int smoothing = 3, int firstLag = 1, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.AutocorrelationReversalValues(bars, length, smoothing, firstLag, Kind(kind)); var values = expected.Outputs["Eacr"]; var batch = Data(bars).CalculateEhlersAutoCorrelationReversals(kind, length, smoothing, firstLag); Assert.Equal(new[] { "Eacr" }, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(values, batch.CustomValuesList); Equal(values, batch.OutputValues["Eacr"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersAutoCorrelationReversalsFast(Data(bars), context, length, smoothing, firstLag, kind); Equal(values, fast.ToArray());
        using var state = new EhlersAutoCorrelationReversalsState(kind, length, smoothing, firstLag);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Noisy(20)) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(values[i], actual.Value); Assert.Equal(actual.Value, actual.Outputs!["Eacr"]); } }
        }
        return values;
    }
    [Fact]
    public void NonzeroCrossingsRetainWideAndSubnormalSignals()
    {
        var bars = Noisy(); Assert.Contains(1d, Check(bars)); Assert.Contains(0d, Check(bars));
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.WildersSmoothingMethod })
            Check(bars.Select(b => Candle(b.Close * scale)).ToArray(), kind: kind);
        foreach (var lag in new[] { 1, 2, 3, 5 }) Check(bars, 5, 3, lag);
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsKeepLazyHistories()
    {
        Check(Array.Empty<Bar>()); Assert.All(Check(Enumerable.Repeat(Candle(0), 65).ToArray()), value => Assert.Equal(0, value));
        foreach (var periods in new[] { (int.MinValue, 0, -1), (1, 1, 1), (2, 1, 0), (5, 3, 5), (5, 3, 6), (5, 3, int.MaxValue), (int.MaxValue, 3, 1), (2, int.MaxValue, 1) })
        { var values = Check(Noisy(40), periods.Item1, periods.Item2, periods.Item3); if (periods.Item1 <= 1 || periods.Item1 == int.MaxValue || periods.Item3 > periods.Item1) Assert.All(values, value => Assert.Equal(0, value)); }
    }
    [Fact]
    public void ReflectionAndPowerOfTwoScalingPreserveReversals()
    {
        var bars = Noisy(); var expected = Check(bars);
        foreach (var scale in new[] { -1d, Math.Pow(2, -500), Math.Pow(2, 500) }) Equal(expected, Check(bars.Select(b => Candle(b.Close * scale)).ToArray()));
    }
    [Fact]
    public void SelectedPricesReachBatchAndFast()
    {
        var selected = Noisy().Select(b => b.Close).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.AutocorrelationReversalValues(selected.Select(v => Candle(v)).ToArray(), 2, 3, 1); Assert.Contains(1d, expected.Outputs["Eacr"]);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersAutoCorrelationReversals(length1: 2, length2: 3, length3: 1); Equal(expected.Outputs["Eacr"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersAutoCorrelationReversalsFast(source, context, 2, 3, 1, MovingAvgType.ExponentialMovingAverage); Equal(expected.Outputs["Eacr"], result.ToArray());
    }
    [Fact]
    public void CustomerAverageIsConsumedOnceAndControlsOnlyDirection()
    {
        var bars = Noisy(); var prices = bars.Select(b => b.Close).ToArray(); var mean = prices.Select((_, i) => i % 2 == 0 ? 1000d : -1000d).ToArray(); var expected = BuiltInFormulaReferences.AutocorrelationReversalValues(bars, 2, 3, 1, externalAverage: mean); Assert.Contains(Signal.Buy, expected.Signals); Assert.Contains(Signal.Sell, expected.Signals);
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(3, period); Assert.Equal(prices, values); return mean; });
            if (batch) { var actual = Data(bars).CalculateEhlersAutoCorrelationReversals(length1: 2, length2: 3, length3: 1); Equal(expected.Outputs["Eacr"], actual.CustomValuesList); Assert.Equal(expected.Signals, actual.SignalsList); }
            else { using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeEhlersAutoCorrelationReversalsFast(Data(bars), context, 2, 3, 1, MovingAvgType.ExponentialMovingAverage); Equal(expected.Outputs["Eacr"], actual.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceCrossingsOrCorrelation()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersAutoCorrelationReversalsState(length1: 2, length2: 3, length3: 1); using var control = new EhlersAutoCorrelationReversalsState(length1: 2, length2: 3, length3: 1);
            foreach (var bar in Noisy(25)) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            foreach (var bar in Noisy(20)) { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Eacr"], actual.Outputs!["Eacr"]); }
        }
    }
}
