using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MamaDerivedNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close) => new(DateTime.UnixEpoch, 0, 4, -4, close, 1);
    private static int Mode(IIndicator indicator) => indicator is EhlersSineWaveIndicatorV1 ? 0 : indicator is EhlersHilbertOscillator ? 1 : 2;
    private static IStreamingIndicatorState State(int mode, int length = 7) => mode == 0 ? new EhlersSineWaveIndicatorV1State() : mode == 1 ? new EhlersHilbertOscillatorState(length) : new EhlersInstantaneousTrendlineV1State();
    private static StockData Batch(StockData data, int mode, int length = 7) => mode == 0 ? data.CalculateEhlersSineWaveIndicatorV1() : mode == 1 ? data.CalculateEhlersHilbertOscillator(length) : data.CalculateEhlersInstantaneousTrendlineV1();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSineWaveIndicatorV1) || c.IndicatorType == typeof(EhlersHilbertOscillator) || c.IndicatorType == typeof(EhlersInstantaneousTrendlineV1)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCycleProjection(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MamaDerivedValues(bars, Mode(c.Factory())).Outputs, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, int mode, int length = 7)
    {
        var expected = BuiltInFormulaReferences.MamaDerivedValues(bars, mode); var batch = Batch(Data(bars), mode, length); Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList);
        if (mode == 1) Assert.Empty(batch.CustomValuesList); else Assert.Equal(expected.Outputs.First().Value, batch.CustomValuesList);
        using var context = new ComputeContext();
        foreach (var key in expected.Outputs.Keys)
        {
            Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var result = IndicatorCompute.ComputeMamaDerivedFast(Data(bars), context, mode, key); Assert.Equal(expected.Outputs[key], result.ToArray());
            if (mode == 1) { using var legacy = IndicatorCompute.ComputeEhlersHilbertOscillatorFast(Data(bars), context, length, key == "I3" ? IndicatorCompute.EhlersHilbertOutput.InPhase : IndicatorCompute.EhlersHilbertOutput.Quadrature); Assert.Equal(expected.Outputs[key], legacy.ToArray()); }
        }
        var primary = mode == 1 ? "IQ" : expected.Outputs.Keys.First(); using var defaultResult = IndicatorCompute.ComputeMamaDerivedFast(Data(bars), context, mode, null); Assert.Equal(expected.Outputs[primary], defaultResult.ToArray());
        var state = State(mode, length);
        try
        {
            for (var replay = 0; replay < 2; replay++)
            {
                for (var i = 0; i < 60; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(Candle(-double.MaxValue)), false, false);
                    foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Outputs[primary][i], point.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); }
                }
            }
        }
        finally { ((IDisposable)state).Dispose(); }
        return expected.Outputs;
    }
    [Fact]
    public void WideAndSubnormalPricesPreserveAllOutputsAndSignals()
    {
        foreach (var mode in Enumerable.Range(0, 3)) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) Check(Enumerable.Range(0, 95).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(), mode);
        var slowCycle = Enumerable.Range(0, 75).Select(i => Candle(Math.Sin(i * .03844335937500001))).ToArray();
        Assert.Contains(BuiltInFormulaReferences.MamaValues(slowCycle.Select(b => b.Close).ToArray()).Outputs["SmoothPeriod"].Select((period, index) => Math.Ceiling(period + .5) > index + 1), padded => padded);
        Check(slowCycle, 2);
    }
    [Fact]
    public void StartupSilenceAndHilbertLengthRetainOriginalConventions()
    {
        foreach (var mode in Enumerable.Range(0, 3)) { Check(Array.Empty<Bar>(), mode); Check(Enumerable.Repeat(Candle(0), 70).ToArray(), mode); }
        var trend = Check(new[] { Candle(10), Candle(20), Candle(30) }, 2); Assert.Equal(10, trend["Eit"][0]); Assert.Equal(4, trend["Signal"][0]);
        var bars = Enumerable.Range(0, 85).Select(i => Candle(Math.Sin(i * .21))).ToArray(); var normal = Check(bars, 1);
        foreach (var length in new[] { int.MinValue, 0, 1, 23, int.MaxValue }) { var actual = Check(bars, 1, length); foreach (var key in normal.Keys) Assert.Equal(normal[key], actual[key]); }
    }
    [Fact]
    public void SineRetainsAbsolutePhaseThresholdAndFullFourierHistory()
    {
        var normal = Check(Enumerable.Range(0, 100).Select(i => Candle(Math.Sin(i * .13))).ToArray(), 0); var tiny = Check(Enumerable.Range(0, 100).Select(i => Candle(Math.Sin(i * .13) * 1e-8)).ToArray(), 0);
        Assert.Contains(normal["Sine"].Zip(tiny["Sine"]), pair => Math.Abs(pair.First - pair.Second) > .1); Assert.Contains(normal["Sine"].Skip(40), v => Math.Abs(v) > .2);
    }
    [Fact]
    public void SelectedPricesReachBatchAndBothFastEntryPoints()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var mode in Enumerable.Range(0, 3))
        {
            var expected = BuiltInFormulaReferences.MamaDerivedValues(selected.Select(Candle).ToArray(), mode); var data = Data(bars); data.SetCustomValues(selected); Batch(data, mode); Assert.Equal(expected.Signals, data.SignalsList);
            foreach (var key in expected.Outputs.Keys)
            {
                Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeMamaDerivedFast(source, context, mode, key); Assert.Equal(expected.Outputs[key], result.ToArray());
                if (mode == 1) { using var legacy = IndicatorCompute.ComputeEhlersHilbertOscillatorFast(source, context, output: key == "I3" ? IndicatorCompute.EhlersHilbertOutput.InPhase : IndicatorCompute.EhlersHilbertOutput.Quadrature); Assert.Equal(expected.Outputs[key], legacy.ToArray()); }
            }
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceCycleOrProjectionHistory()
    {
        foreach (var mode in Enumerable.Range(0, 3)) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(mode); var control = State(mode);
            try
            {
                for (var i = 0; i < 35; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
                var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
                for (var i = 0; i < 15; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
            }
            finally { ((IDisposable)state).Dispose(); ((IDisposable)control).Dispose(); }
        }
    }
}
