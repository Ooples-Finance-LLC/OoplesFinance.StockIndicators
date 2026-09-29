using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HilbertNoiseNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = -4) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar {i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSignalToNoiseRatioV1)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentHilbertEnergy(IndicatorValidationCase c, string route)
    {
        var options = (EhlersSignalToNoiseRatioV1SpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.HilbertNoiseValues(bars, options.Length, Kind(options.MaType)).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 7, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.HilbertNoiseValues(bars, length, Kind(kind)); var batch = Data(bars).CalculateEhlersSignalToNoiseRatioV1(kind, length); Assert.Equal(new[] { "Esnr" }, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); var values = expected.Outputs["Esnr"]; Equal(values, batch.CustomValuesList); Equal(values, batch.OutputValues["Esnr"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersSignalToNoiseRatioV1Fast(Data(bars), context, length, kind); Equal(values, fast.ToArray()); using var state = new EhlersSignalToNoiseRatioV1State(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            for (var i = 0; i < 60; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.True(IndicatorErrorBudget.Exact.Accepts(values[i], point.Value)); Assert.True(IndicatorErrorBudget.Exact.Accepts(values[i], point.Outputs!["Esnr"])); } }
        }
        return values;
    }
    [Fact]
    public void WideAndSubnormalEnergyRetainsFiniteNoiseRatios()
    {
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.SimpleMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) { var values = Check(Enumerable.Range(0, 90).Select(i => Candle(Math.Sin(i * .27) * scale, scale, -scale)).ToArray(), 7, kind); Assert.All(values, value => Assert.False(double.IsNaN(value) || double.IsInfinity(value))); }
    }
    [Fact]
    public void ExtremeExponentRatiosPreserveLogarithmsAndDirectionalSignals()
    {
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage }) foreach (var pair in new[] { (Math.Pow(2, 1000), double.Epsilon), (Math.Pow(2, -1000), double.MaxValue) }) Check(Enumerable.Range(0, 95).Select(i => Candle(Math.Sin(i * .21) * pair.Item1, pair.Item2, -pair.Item2)).ToArray(), 3, kind);
    }
    [Fact]
    public void EmptyZeroAndSignedRangesPreserveStartupConventions()
    {
        Check(Array.Empty<Bar>()); Assert.All(Check(Enumerable.Repeat(Candle(0, 0, 0), 75).ToArray()), value => Assert.Equal(0, value)); var first = Check(Enumerable.Repeat(Candle(0, 1, -1), 75).ToArray()); Assert.Equal(.475, first[0]);
        Check(Enumerable.Range(0, 75).Select(i => Candle(Math.Sin(i * .21), -2, 2)).ToArray());
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.SimpleMovingAverage }) foreach (var length in new[] { int.MinValue, 0, 1, 31, int.MaxValue }) Check(Enumerable.Range(0, 75).Select(i => Candle(Math.Sin(i * .21))).ToArray(), length, kind);
    }
    [Fact]
    public void PowerOfTwoPriceAndRangeScalingPreservesDecibels()
    {
        var prices = Enumerable.Range(0, 95).Select(i => (double)(i % 9 - 4)).ToArray(); var normal = Check(prices.Select(v => Candle(v, 5, -5)).ToArray()); foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) Equal(normal, Check(prices.Select(v => Candle(v * scale, 5 * scale, -5 * scale)).ToArray()));
    }
    [Fact]
    public void SelectedPricesReachBatchAndFastWithoutReplacingRanges()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100, 7, -2)).ToArray(); var projected = selected.Select(v => Candle(v, 7, -2)).ToArray();
        var expected = BuiltInFormulaReferences.HilbertNoiseValues(projected, 7); var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersSignalToNoiseRatioV1(); Assert.Equal(expected.Signals, data.SignalsList); Equal(expected.Outputs["Esnr"], data.CustomValuesList); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersSignalToNoiseRatioV1Fast(source, context); Equal(expected.Outputs["Esnr"], result.ToArray());
    }
    [Fact]
    public void CustomerAverageIsConsumedOnceAndControlsOnlyDirection()
    {
        var prices = Enumerable.Range(0, 80).Select(i => 100 * Math.Sin(i * .3)).ToArray(); var bars = prices.Select(v => Candle(v, .1, 0)).ToArray(); var mean = prices.Select((_, i) => i % 2 == 0 ? 1000d : -1000d).ToArray(); var expected = BuiltInFormulaReferences.HilbertNoiseValues(bars, 3, externalAverage: mean);
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(3, period); Assert.Equal(prices, values); return mean; });
            if (batch) { var actual = Data(bars).CalculateEhlersSignalToNoiseRatioV1(length: 3); Equal(expected.Outputs["Esnr"], actual.CustomValuesList); Assert.Equal(expected.Signals, actual.SignalsList); }
            else { using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeEhlersSignalToNoiseRatioV1Fast(Data(bars), context, 3); Equal(expected.Outputs["Esnr"], actual.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceHilbertEnergyOrAverages()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersSignalToNoiseRatioV1State(); using var control = new EhlersSignalToNoiseRatioV1State();
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 15; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Esnr"], actual.Outputs!["Esnr"]); }
        }
    }
}
