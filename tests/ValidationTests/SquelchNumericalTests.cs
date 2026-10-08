using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SquelchNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSquelchIndicator)).Select(c => new object[] { c });
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
    { var o = (EhlersSquelchIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Esi", BuiltInFormulaReferences.SquelchValues(bars, o.Length1, o.Length2, o.Length3).Values } }; }
    private static double[] Check(Bar[] bars, int lag = 6, int threshold = 10, int horizon = 40)
    {
        var expected = BuiltInFormulaReferences.SquelchValues(bars, lag, threshold, horizon); var batch = Data(bars).CalculateEhlersSquelchIndicator(lag, threshold, horizon); Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Values, batch.OutputValues["Esi"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersSquelchIndicatorFast(Data(bars), context, lag, threshold, horizon); Assert.Equal(expected.Values, fast.ToArray());
        using var state = new EhlersSquelchIndicatorState(lag, threshold, horizon);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in Enumerable.Range(0, 55).Select(i => Math.Sin(i * .3))) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Esi"]); }
            }
        }
        return expected.Values;
    }
    [Fact]
    public void WideAndSubnormalPricesPreservePhaseCyclesAndSignals()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        {
            var bars = Enumerable.Range(0, 130).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(); var values = Check(bars); Assert.Contains(0d, values); Assert.Contains(1d, values);
        }
    }
    [Fact]
    public void PowerOfTwoScalingRetainsCycleAndDirectionalSignals()
    {
        var prices = Enumerable.Range(0, 100).Select(i => (double)((i % 16 <= 8 ? i % 16 : 16 - i % 16) - 4)).ToArray(); var baseline = Check(prices.Select(v => Candle(v)).ToArray()); var signals = Data(prices.Select(v => Candle(v)).ToArray()).CalculateEhlersSquelchIndicator(6, 10, 40).SignalsList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var bars = prices.Select(v => Candle(v * scale)).ToArray(); Assert.Equal(baseline, Check(bars)); Assert.Equal(signals, Data(bars).CalculateEhlersSquelchIndicator(6, 10, 40).SignalsList); }
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyConsumedHistory()
    {
        var bars = Enumerable.Range(0, 30).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var period in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, period, period, period); Check(Array.Empty<Bar>(), period, period, period); Check(bars, 2, 1, period); }
    }
    [Fact]
    public void StrictFullCycleCrossingIncludesLagThreeHundredSixty()
    {
        var bars = Enumerable.Repeat(Candle(3), 400).ToArray(); Assert.All(Check(bars, 1, 1, 359), v => Assert.Equal(0, v));
        var baseline = Check(bars, 1, 1, 360); Assert.All(baseline.Take(360), v => Assert.Equal(0, v)); Assert.All(baseline.Skip(360), v => Assert.Equal(1, v));
        Assert.Equal(baseline, Check(bars, 1, 1, 361)); Assert.Equal(baseline, Check(bars, 1, 1, int.MaxValue));
    }
    [Fact]
    public void ShortHorizonsCannotInventACompleteCycle()
    {
        var bars = Enumerable.Range(0, 90).Select(i => Candle(Math.Sin(i * 1.7))).ToArray();
        foreach (var horizon in new[] { 1, 2, 4, 5 }) Assert.All(Check(bars, 2, 1, horizon), v => Assert.Equal(0, v));
    }
    [Fact]
    public void SignalsRetainOrderingBeyondThePublishedPriceRange()
    {
        var prices = Enumerable.Range(0, 75).Select(i => i % 4 < 2 ? double.MaxValue : -double.MaxValue).ToArray(); Check(prices.Select(v => Candle(v)).ToArray(), 1, 1, 40);
        var expected = BuiltInFormulaReferences.SquelchValues(prices.Select(v => Candle(v)).ToArray(), 1, 1, 40); Assert.True(expected.Signals.Distinct().Count() >= 3);
    }
    [Fact]
    public void SelectedPricesReachFastAndBatchPhaseCalculations()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.SquelchValues(selected.Select(v => Candle(v)).ToArray(), 3, 10, 40);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersSquelchIndicator(3, 10, 40); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        data = Data(bars); data.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersSquelchIndicatorFast(data, context, 3, 10, 40); Assert.Equal(expected.Values, result.ToArray());
    }
    [Fact]
    public void InvalidFieldsDoNotAdvancePhaseOrCycleHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersSquelchIndicatorState(3, 10, 40); using var control = new EhlersSquelchIndicatorState(3, 10, 40);
            for (var i = 0; i < 50; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 50; i++) { var bar = Native(Candle(Math.Sin(i * .4))); Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value); }
        }
    }
}
