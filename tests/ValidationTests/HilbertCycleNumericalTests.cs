using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HilbertCycleNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersDualDifferentiatorDominantCycle) || c.IndicatorType == typeof(EhlersHomodyneDominantCycle) || c.IndicatorType == typeof(EhlersPhaseAccumulationDominantCycle)).Select(c => new object[] { c });
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
    private static string Key(int mode) => mode == 0 ? "Edddc" : mode == 1 ? "Ehdc" : "Epadc";
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    {
        var options = ((IBuiltInIndicator)indicator).CreateOptions(); int mode, upper, lower, minimum, horizon = 1;
        if (options is EhlersDualDifferentiatorDominantCycleSpecOptions dual) { mode = 0; upper = dual.Length1; lower = dual.Length2; minimum = dual.Length3; }
        else if (options is EhlersHomodyneDominantCycleSpecOptions homodyne) { mode = 1; upper = homodyne.Length1; lower = homodyne.Length2; minimum = homodyne.Length3; }
        else { var phase = (EhlersPhaseAccumulationDominantCycleSpecOptions)options; mode = 2; upper = phase.Length1; lower = phase.Length2; minimum = phase.Length3; horizon = phase.Length4; }
        return new() { { Key(mode), BuiltInFormulaReferences.HilbertCycleValues(bars, upper, lower, minimum, horizon, mode).Values } };
    }
    private static StockData Batch(StockData data, int mode, int upper, int lower, int minimum, int horizon) => mode == 0 ? data.CalculateEhlersDualDifferentiatorDominantCycle(upper, lower, minimum) : mode == 1 ? data.CalculateEhlersHomodyneDominantCycle(upper, lower, minimum) : data.CalculateEhlersPhaseAccumulationDominantCycle(upper, lower, minimum, horizon);
    private static IStreamingIndicatorState State(int mode, int upper, int lower, int minimum, int horizon) => mode == 0 ? new EhlersDualDifferentiatorDominantCycleState(upper, lower, minimum) : mode == 1 ? new EhlersHomodyneDominantCycleState(upper, lower, minimum) : new EhlersPhaseAccumulationDominantCycleState(upper, lower, minimum, horizon);
    private static double[] Check(Bar[] bars, int mode, int upper = 48, int lower = 20, int minimum = 8, int horizon = 40)
    {
        var expected = BuiltInFormulaReferences.HilbertCycleValues(bars, upper, lower, minimum, horizon, mode); var batch = Batch(Data(bars), mode, upper, lower, minimum, horizon);
        Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Values, batch.OutputValues[Key(mode)]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeHilbertCycleFast(Data(bars), context, upper, lower, minimum, horizon, mode); Assert.Equal(expected.Values, result.ToArray());
        var state = State(mode, upper, lower, minimum, horizon); using var lifetime = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in Enumerable.Range(0, 55).Select(i => Math.Sin(i * .3))) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs![Key(mode)]); }
            }
        }
        return expected.Values;
    }
    [Fact]
    public void WideAndSubnormalPricesRetainCycleMeasurements()
    {
        foreach (var mode in new[] { 0, 1, 2 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        { var values = Check(Enumerable.Range(0, 100).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(), mode); Assert.All(values, value => Assert.True(double.IsFinite(value))); Assert.Contains(values, value => value != 0); }
    }
    [Fact]
    public void PhaseWindowIncludesTheFullThreeHundredSixtySampleCrossing()
    {
        var bars = Enumerable.Repeat(Candle(0), 390).ToArray(); var shortWindow = Check(bars, 2, minimum: 1, horizon: 359); Assert.All(shortWindow, value => Assert.Equal(0, value));
        var full = Check(bars, 2, minimum: 1, horizon: 360); Assert.All(full.Take(359), value => Assert.Equal(0, value)); Assert.True(full[359] > 0); Assert.Equal(full, Check(bars, 2, minimum: 1, horizon: int.MaxValue));
        Assert.All(Check(bars, 2, minimum: 1, horizon: 1), value => Assert.Equal(0, value));
    }
    [Fact]
    public void IndependentExtremePeriodsPreserveBoundsAndSmoothing()
    {
        var bars = Enumerable.Range(0, 50).Select(i => Candle(i % 7 - 3)).ToArray();
        foreach (var mode in new[] { 0, 1, 2 }) foreach (var period in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, mode, upper: period); Check(bars, mode, lower: period); Check(bars, mode, minimum: period); Check(bars, mode, horizon: period); Check(Array.Empty<Bar>(), mode, period, period, period, period); }
        foreach (var mode in new[] { 0, 1, 2 }) Check(Enumerable.Repeat(Candle(0), 90).ToArray(), mode);
    }
    [Fact]
    public void ExactPowerOfTwoScalingPreservesOutputsAndSignals()
    {
        var prices = Enumerable.Range(0, 100).Select(i => (double)(i % 9 - 4)).ToArray();
        foreach (var mode in new[] { 0, 1, 2 }) { var baseline = Check(prices.Select(v => Candle(v)).ToArray(), mode); var signals = Batch(Data(prices.Select(v => Candle(v)).ToArray()), mode, 48, 20, 8, 40).SignalsList;
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) { var bars = prices.Select(v => Candle(v * scale)).ToArray(); Assert.Equal(baseline, Check(bars, mode)); Assert.Equal(signals, Batch(Data(bars), mode, 48, 20, 8, 40).SignalsList); }
        }
    }
    [Fact]
    public void SelectedPricesReachEachBatchAndFastRoute()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var mode in new[] { 0, 1, 2 }) { var expected = BuiltInFormulaReferences.HilbertCycleValues(selected.Select(v => Candle(v)).ToArray(), 48, 20, 8, 40, mode); var data = Data(bars); data.SetCustomValues(selected); Batch(data, mode, 48, 20, 8, 40); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Values, data.OutputValues[Key(mode)]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeHilbertCycleFast(source, context, 48, 20, 8, 40, mode); Assert.Equal(expected.Values, result.ToArray()); }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceCycleHistory()
    {
        foreach (var mode in new[] { 0, 1, 2 }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(mode, 48, 20, 8, 40); var control = State(mode, 48, 20, 8, 40); using var lifetime = (IDisposable)state; using var other = (IDisposable)control;
            for (var i = 0; i < 50; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs![Key(mode)], actual.Outputs![Key(mode)]); }
        }
    }
}
