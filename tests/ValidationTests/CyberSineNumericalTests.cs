using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CyberSineNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSineWaveIndicatorV2)).Select(c => new object[] { c });
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
        var options = (EhlersSineWaveIndicatorV2SpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.CyberSineValues(bars, options.Length, options.Alpha).Outputs;
    }
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 5, double alpha = .07)
    {
        var expected = BuiltInFormulaReferences.CyberSineValues(bars, length, alpha); var batch = Data(bars).CalculateEhlersSineWaveIndicatorV2(length, alpha); Assert.Equal(expected.Outputs["Sine"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { using var result = IndicatorCompute.ComputeCyberSineFast(Data(bars), context, length, alpha, key); Assert.Equal(expected.Outputs[key], result.ToArray()); Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); }
        using var state = new EhlersSineWaveIndicatorV2State(length, alpha);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in Enumerable.Range(0, 55).Select(i => Math.Sin(i * .3))) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Outputs["Sine"][i], point.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideAndSubnormalPricesRetainFourierPhaseAndSignals()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        { var values = Check(Enumerable.Range(0, 100).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray()); foreach (var output in values.Values) Assert.All(output, value => Assert.InRange(value, -1d, 1d)); }
    }
    [Fact]
    public void AbsoluteProjectionThresholdStillChangesSmallSignalPhase()
    {
        var prices = Enumerable.Range(0, 100).Select(i => Math.Sin(i * .27)).ToArray(); var ordinary = Check(prices.Select(v => Candle(v)).ToArray()); var tiny = Check(prices.Select(v => Candle(v * 1e-5)).ToArray()); Assert.False(ordinary["Sine"].SequenceEqual(tiny["Sine"]));
        var zeros = Check(Enumerable.Repeat(Candle(0), 90).ToArray()); Assert.All(zeros["Sine"], v => Assert.Equal(1, v)); Assert.All(zeros["LeadSine"], v => Assert.Equal(Math.Sin(135 * (Math.PI / 180)), v));
    }
    [Fact]
    public void TinyPowerOfTwoScalingRetainsPhaseSigns()
    {
        var prices = Enumerable.Range(0, 90).Select(i => (double)(i % 9 - 4)).ToArray(); var baseline = Check(prices.Select(v => Candle(v * Math.Pow(2, -1000))).ToArray()); var actual = Check(prices.Select(v => Candle(v * double.Epsilon)).ToArray()); foreach (var key in baseline.Keys) Assert.Equal(baseline[key], actual[key]);
    }
    [Fact]
    public void ExtremePeriodsAndIndependentAlphaRemainFinite()
    {
        var bars = Enumerable.Range(0, 40).Select(i => Candle(i % 7 - 3)).ToArray(); foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, length); Check(Array.Empty<Bar>(), length); }
        foreach (var alpha in new[] { -.5, 0, .2, 1, 2, double.MaxValue }) Check(bars.Take(24).ToArray(), alpha: alpha);
    }
    [Fact]
    public void SelectedPricesReachBatchAndBothFastOutputs()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.CyberSineValues(selected.Select(v => Candle(v)).ToArray(), 5, .07); var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersSineWaveIndicatorV2(); Assert.Equal(expected.Signals, data.SignalsList);
        foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeCyberSineFast(source, context, 5, .07, key); Assert.Equal(expected.Outputs[key], result.ToArray()); }
    }
    [Fact]
    public void InvalidFieldsCannotAdvancePeriodOrProjectionHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersSineWaveIndicatorV2State(); using var control = new EhlersSineWaveIndicatorV2State();
            for (var i = 0; i < 50; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void NonfiniteAlphaIsRejectedAtConstruction()
    { foreach (var alpha in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersSineWaveIndicatorV2State(alpha: alpha)); }
}
