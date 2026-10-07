using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HilbertTransformerNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersHilbertTransformer) || c.IndicatorType == typeof(EhlersHilbertTransformerIndicator)).Select(c => new object[] { c });
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
    {
        var options = ((IBuiltInIndicator)indicator).CreateOptions();
        if (options is EhlersHilbertTransformerIndicatorSpecOptions filtered) return BuiltInFormulaReferences.HilbertTransformerValues(bars, filtered.Length1, filtered.Length2, filtered.Length3, true).Outputs;
        var basic = (EhlersHilbertTransformerSpecOptions)options; return BuiltInFormulaReferences.HilbertTransformerValues(bars, basic.Length1, basic.Length2, 1, false).Outputs;
    }
    private static StockData Batch(StockData data, bool smooth, int upper, int lower, int smoothing) => smooth ? data.CalculateEhlersHilbertTransformerIndicator(upper, lower, smoothing) : data.CalculateEhlersHilbertTransformer(upper, lower);
    private static IStreamingIndicatorState State(bool smooth, int upper, int lower, int smoothing) => smooth ? new EhlersHilbertTransformerIndicatorState(upper, lower, smoothing) : new EhlersHilbertTransformerState(upper, lower);
    private static Dictionary<string, double[]> Check(Bar[] bars, bool smooth, int upper = 48, int lower = 20, int smoothing = 10)
    {
        var expected = BuiltInFormulaReferences.HilbertTransformerValues(bars, upper, lower, smoothing, smooth); var batch = Batch(Data(bars), smooth, upper, lower, smoothing);
        Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { using var result = IndicatorCompute.ComputeHilbertTransformerFast(Data(bars), context, upper, lower, smoothing, smooth, key); Assert.Equal(expected.Outputs[key], result.ToArray()); Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); }
        var state = State(smooth, upper, lower, smoothing); using var lifetime = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in Enumerable.Range(0, 55).Select(i => Math.Sin(i * .3))) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Outputs["Real"][i], point.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideAndSubnormalPricesRetainBothNormalizedTrajectories()
    {
        foreach (var smooth in new[] { false, true }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        {
            var values = Check(Enumerable.Range(0, 100).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(), smooth);
            Assert.All(values["Real"], v => Assert.InRange(v, -1d, 1d)); Assert.Contains(values["Real"], v => v != 0); Assert.Contains(values["Imag"], v => v != 0);
            if (!smooth) Assert.All(values["Imag"], v => Assert.InRange(v, -1d, 1d));
        }
    }
    [Fact]
    public void FirstNonzeroSampleNormalizesBeforePublication()
    {
        foreach (var value in new[] { double.Epsilon, -double.Epsilon, 1d, -1d, double.MaxValue })
        { var values = Check(new[] { Candle(value) }, false); Assert.Equal(Math.Sign(value), values["Real"][0]); Assert.Equal(Math.Sign(value), values["Imag"][0]); }
        foreach (var smooth in new[] { false, true }) foreach (var output in Check(Enumerable.Repeat(Candle(0), 70).ToArray(), smooth).Values) Assert.All(output, value => Assert.Equal(0, value));
    }
    [Fact]
    public void IndependentPeriodsPreserveRoofingAndSmoothingCoefficients()
    {
        var bars = Enumerable.Range(0, 90).Select(i => Candle(i % 7 - 3)).ToArray();
        foreach (var smooth in new[] { false, true }) foreach (var period in new[] { int.MinValue, 0, 1, 2, int.MaxValue })
        { Check(bars, smooth, period, 20, 10); Check(bars, smooth, 48, period, 10); Check(bars, smooth, 48, 20, period); Check(Array.Empty<Bar>(), smooth, period, period, period); }
    }
    [Fact]
    public void ExactPowerOfTwoScalingPreservesNormalizedOutputsAndSignals()
    {
        var prices = Enumerable.Range(0, 100).Select(i => (double)(i % 9 - 4)).ToArray();
        foreach (var smooth in new[] { false, true })
        {
            var baseline = Check(prices.Select(v => Candle(v)).ToArray(), smooth); var signals = Batch(Data(prices.Select(v => Candle(v)).ToArray()), smooth, 48, 20, 10).SignalsList;
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) { var bars = prices.Select(v => Candle(v * scale)).ToArray(); var actual = Check(bars, smooth); foreach (var key in baseline.Keys) Assert.Equal(baseline[key], actual[key]); Assert.Equal(signals, Batch(Data(bars), smooth, 48, 20, 10).SignalsList); }
        }
    }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastOutputs()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var smooth in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.HilbertTransformerValues(selected.Select(v => Candle(v)).ToArray(), 48, 20, 10, smooth); var data = Data(bars); data.SetCustomValues(selected); Batch(data, smooth, 48, 20, 10); Assert.Equal(expected.Signals, data.SignalsList);
            foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeHilbertTransformerFast(source, context, 48, 20, 10, smooth, key); Assert.Equal(expected.Outputs[key], result.ToArray()); }
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceRoofingOrPeakHistory()
    {
        foreach (var smooth in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(smooth, 48, 20, 10); var control = State(smooth, 48, 20, 10); using var lifetime = (IDisposable)state; using var other = (IDisposable)control;
            for (var i = 0; i < 50; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
}
