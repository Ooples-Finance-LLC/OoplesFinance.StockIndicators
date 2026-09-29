using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FourierPhaseNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = -4) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar {i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersPhaseCalculation)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentFourierPhase(IndicatorValidationCase c, string route)
    {
        var options = (EhlersPhaseCalculationSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FourierPhaseValues(bars, options.Length, Kind(options.MaType)).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 15, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.FourierPhaseValues(bars, length, Kind(kind)); var batch = Data(bars).CalculateEhlersPhaseCalculation(kind, length);
        Assert.Equal(new[] { "Phase", "Signal" }, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(expected.Outputs["Phase"], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in new[] { "Phase", "Signal" }) { Equal(expected.Outputs[key], batch.OutputValues[key]); using var fast = IndicatorCompute.ComputeEhlersPhaseCalculationFast(Data(bars), context, length, kind, key == "Signal"); Equal(expected.Outputs[key], fast.ToArray()); }
        using var state = new EhlersPhaseCalculationState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            for (var i = 0; i < 35; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), commit, true); foreach (var key in new[] { "Phase", "Signal" }) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected.Outputs[key][i], actual.Outputs![key]), $"{key} at {i}: {expected.Outputs[key][i]:R} != {actual.Outputs[key]:R}"); Assert.Equal(actual.Value, actual.Outputs!["Phase"]); } }
        }
        return expected.Outputs["Phase"];
    }
    [Fact]
    public void WideAndSubnormalPricesPreserveAnglesAndSmoothedSignals()
    {
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.SimpleMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        { var values = Check(Enumerable.Range(0, 75).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(), 7, kind); Assert.All(values, value => Assert.InRange(value, 0, 359.99999999999999)); }
    }
    [Fact]
    public void EmptyFlatAndExtremePeriodsPreserveStartup()
    {
        Check(Array.Empty<Bar>()); foreach (var value in new[] { 0d, double.MaxValue, -double.MaxValue, double.Epsilon }) Assert.All(Check(Enumerable.Repeat(Candle(value), 35).ToArray(), 7).Skip(6), phase => Assert.Equal(90, phase));
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage }) foreach (var length in new[] { int.MinValue, 0, 1, 2, 31, int.MaxValue }) Check(Enumerable.Range(0, 45).Select(i => Candle(Math.Sin(i * .3))).ToArray(), length, kind);
    }
    [Fact]
    public void PowerOfTwoScalingAndCompleteWindowOffsetsPreservePhase()
    {
        var prices = Enumerable.Range(0, 70).Select(i => (double)(i % 9 - 4)).ToArray(); var expected = Check(prices.Select(v => Candle(v)).ToArray(), 7);
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) Equal(expected, Check(prices.Select(v => Candle(v * scale)).ToArray(), 7));
        Equal(expected.Skip(6).ToArray(), Check(prices.Select(v => Candle(v + 1024)).ToArray(), 7).Skip(6));
    }
    [Fact]
    public void QuadrantsAndNegligibleVectorsRetainConventions()
    {
        Equal(new[] { 90d, 180, 270, 0, 90 }, Check(new[] { 1d, 0, -1, 0, 1 }.Select(v => Candle(v)).ToArray(), 4, MovingAvgType.SimpleMovingAverage));
        var alternating = Check(Enumerable.Range(0, 70).Select(i => Candle(i % 2 == 0 ? 1 : -1)).ToArray(), 4); Assert.All(alternating.Skip(3), value => Assert.Equal(90, value));
    }
    [Fact]
    public void SelectedPricesReachBatchAndBothFastOutputs()
    {
        var selected = Enumerable.Range(0, 75).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.FourierPhaseValues(selected.Select(v => Candle(v)).ToArray(), 7);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersPhaseCalculation(length: 7); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Phase", "Signal" }) { Equal(expected.Outputs[key], batch.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersPhaseCalculationFast(source, context, 7, signal: key == "Signal"); Equal(expected.Outputs[key], result.ToArray()); }
    }
    [Fact]
    public void CustomerAverageReceivesPhasesOnceAndControlsSignals()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(Math.Sin(i * .3))).ToArray(); var means = Enumerable.Range(0, bars.Length).Select(i => i % 2 == 0 ? 400d : -10d).ToArray(); var expected = BuiltInFormulaReferences.FourierPhaseValues(bars, 7, externalAverage: means);
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(7, period); Equal(expected.Outputs["Phase"], values); return means; });
            if (batch) { var actual = Data(bars).CalculateEhlersPhaseCalculation(length: 7); Equal(means, actual.OutputValues["Signal"]); Assert.Equal(expected.Signals, actual.SignalsList); }
            else { using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeEhlersPhaseCalculationFast(Data(bars), context, 7, signal: true); Equal(means, actual.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvancePhaseOrSmoothing()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersPhaseCalculationState(); using var control = new EhlersPhaseCalculationState();
            for (var i = 0; i < 25; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 15; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); foreach (var key in new[] { "Phase", "Signal" }) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
}
