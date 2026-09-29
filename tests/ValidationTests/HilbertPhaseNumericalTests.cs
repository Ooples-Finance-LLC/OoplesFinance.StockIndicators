using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HilbertPhaseNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersHilbertTransformIndicator) || c.IndicatorType == typeof(EhlersInstantaneousPhaseIndicator)).Select(c => new object[] { c });
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
        if (options is EhlersInstantaneousPhaseIndicatorSpecOptions phase) return BuiltInFormulaReferences.HilbertPhaseValues(bars, phase.Length1, .635, .338, phase.Length2, true).Outputs;
        var hilbert = (EhlersHilbertTransformIndicatorSpecOptions)options; return BuiltInFormulaReferences.HilbertPhaseValues(bars, hilbert.Length, hilbert.IMult, hilbert.QMult, 1, false).Outputs;
    }
    private static StockData Batch(StockData data, bool phase, int length, int horizon, double realGain, double imaginaryGain) => phase ? data.CalculateEhlersInstantaneousPhaseIndicator(length, horizon) : data.CalculateEhlersHilbertTransformIndicator(length, realGain, imaginaryGain);
    private static IStreamingIndicatorState State(bool phase, int length, int horizon, double realGain, double imaginaryGain) => phase ? new EhlersInstantaneousPhaseIndicatorState(length, horizon) : new EhlersHilbertTransformIndicatorState(length, realGain, imaginaryGain);
    private static Dictionary<string, double[]> Check(Bar[] bars, bool phase, int length = 6, int horizon = 40, double realGain = .635, double imaginaryGain = .338)
    {
        var expected = BuiltInFormulaReferences.HilbertPhaseValues(bars, length, realGain, imaginaryGain, horizon, phase); var batch = Batch(Data(bars), phase, length, horizon, realGain, imaginaryGain); var primary = phase ? "Eipi" : "Quad";
        if (phase) Assert.Equal(expected.Outputs[primary], batch.CustomValuesList); else Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { using var result = IndicatorCompute.ComputeHilbertPhaseFast(Data(bars), context, length, realGain, imaginaryGain, horizon, phase, key); Assert.Equal(expected.Outputs[key], result.ToArray()); Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); }
        var state = State(phase, length, horizon, realGain, imaginaryGain); using var lifetime = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in Enumerable.Range(0, 55).Select(i => Math.Sin(i * .3))) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Outputs[primary][i], point.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideAndSubnormalPricesRetainHilbertComponentsAndCycles()
    {
        foreach (var phase in new[] { false, true }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        {
            var values = Check(Enumerable.Range(0, 100).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(), phase);
            if (phase) Assert.Contains(values["Eipi"], v => v != 0);
        }
    }
    [Fact]
    public void ZeroFeedbackExposesExactLagTwoAndLagFourStartup()
    {
        var values = Check(new[] { 1d, 2, 4, 8, 16, 32 }.Select(v => Candle(v)).ToArray(), false, 1, realGain: 0, imaginaryGain: 0);
        Assert.Equal(new[] { 0d, 0, 0, 1, 2, 4 }, values["Quad"]); Assert.Equal(new[] { 0d, 0, 0, 0, 0, 1.25 }, values["Inphase"]);
    }
    [Fact]
    public void IndependentFeedbackCoefficientsReachBothComponents()
    {
        var bars = Enumerable.Range(0, 24).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var pair in new[] { (0d, 0d), (-.5, .25), (.75, -.5), (2d, 1.5), (double.MaxValue, double.MaxValue) }) Check(bars, false, 1, realGain: pair.Item1, imaginaryGain: pair.Item2);
    }
    [Fact]
    public void ExtremePeriodsAndStrictCycleBoundaryUseBoundedHistory()
    {
        var bars = Enumerable.Range(0, 25).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var phase in new[] { false, true }) foreach (var period in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, phase, period, period); Check(Array.Empty<Bar>(), phase, period, period); }
        var constant = Enumerable.Repeat(Candle(3), 400).ToArray(); Assert.All(Check(constant, true, 1, 359)["Eipi"], v => Assert.Equal(0, v)); var baseline = Check(constant, true, 1, 360)["Eipi"]; Assert.Equal(0, baseline[359]); Assert.Equal(90, baseline[360]); Assert.Equal(baseline, Check(constant, true, 1, int.MaxValue)["Eipi"]);
    }
    [Fact]
    public void PowerOfTwoScalingPreservesMeasuredCycleAndSignals()
    {
        var prices = Enumerable.Range(0, 100).Select(i => (double)((i % 16 <= 8 ? i % 16 : 16 - i % 16) - 4)).ToArray(); var baseline = Check(prices.Select(v => Candle(v)).ToArray(), true)["Eipi"]; var signals = Batch(Data(prices.Select(v => Candle(v)).ToArray()), true, 6, 40, .635, .338).SignalsList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) { var bars = prices.Select(v => Candle(v * scale)).ToArray(); Assert.Equal(baseline, Check(bars, true)["Eipi"]); Assert.Equal(signals, Batch(Data(bars), true, 6, 40, .635, .338).SignalsList); }
    }
    [Fact]
    public void SelectedPricesReachEveryBatchAndDirectFastOutput()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var phase in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.HilbertPhaseValues(selected.Select(v => Candle(v)).ToArray(), 3, .635, .338, 40, phase); var data = Data(bars); data.SetCustomValues(selected); Batch(data, phase, 3, 40, .635, .338); Assert.Equal(expected.Signals, data.SignalsList);
            foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeHilbertPhaseFast(source, context, 3, .635, .338, 40, phase, key); Assert.Equal(expected.Outputs[key], result.ToArray()); }
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceHilbertOrPhaseState()
    {
        foreach (var phase in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(phase, 3, 40, .635, .338); var control = State(phase, 3, 40, .635, .338); using var lifetime = (IDisposable)state; using var other = (IDisposable)control;
            for (var i = 0; i < 50; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void NonfiniteFeedbackCoefficientsAreRejectedAtConstruction()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) { Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersHilbertTransformIndicatorState(iMult: value)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersHilbertTransformIndicatorState(qMult: value)); }
    }
}
