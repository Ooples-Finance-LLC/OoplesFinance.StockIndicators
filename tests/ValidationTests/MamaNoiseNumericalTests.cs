using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MamaNoiseNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = -4) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    private static int Mode(IIndicator indicator) => indicator is EhlersAlternateSignalToNoiseRatio ? 0 : indicator is EhlersSignalToNoiseRatioV2 ? 1 : 2;
    private static IStreamingIndicatorState State(int mode, int length) => mode == 0 ? new EhlersAlternateSignalToNoiseRatioState(length) : mode == 1 ? new EhlersSignalToNoiseRatioV2State(length) : new EhlersEnhancedSignalToNoiseRatioState(length);
    private static StockData Batch(StockData data, int mode, int length) => mode == 0 ? data.CalculateEhlersAlternateSignalToNoiseRatio(length) : mode == 1 ? data.CalculateEhlersSignalToNoiseRatioV2(length) : data.CalculateEhlersEnhancedSignalToNoiseRatio(length);
    private static bool Matches(double expected, double actual) => double.IsInfinity(expected) ? expected.Equals(actual) : IndicatorErrorBudget.Exact.Accepts(expected, actual);
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(Matches(expected[i], values[i]), $"bar {i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAlternateSignalToNoiseRatio) || c.IndicatorType == typeof(EhlersSignalToNoiseRatioV2) || c.IndicatorType == typeof(EhlersEnhancedSignalToNoiseRatio)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentEnergyRatios(IndicatorValidationCase c, string route)
    {
        var indicator = c.Factory(); var options = ((IBuiltInIndicator)indicator).CreateOptions(); var length = options is EhlersAlternateSignalToNoiseRatioSpecOptions alternate ? alternate.Length : options is EhlersSignalToNoiseRatioV2SpecOptions v2 ? v2.Length : ((EhlersEnhancedSignalToNoiseRatioSpecOptions)options).Length;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.MamaNoiseValues(bars, length, Mode(indicator)).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, int mode, int length = 6)
    {
        var expected = BuiltInFormulaReferences.MamaNoiseValues(bars, length, mode); var batch = Batch(Data(bars), mode, length); Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(expected.Outputs["Esnr"], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], batch.OutputValues[key]); using var result = IndicatorCompute.ComputeMamaNoiseFast(Data(bars), context, length, mode, key); Equal(expected.Outputs[key], result.ToArray()); }
        if (mode == 1) foreach (var average in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage }) { using var legacy = IndicatorCompute.ComputeEhlersSignalToNoiseRatioV2Fast(Data(bars), context, length, average); Equal(expected.Outputs["Esnr"], legacy.ToArray()); }
        var state = State(mode, length);
        try
        {
            for (var replay = 0; replay < 2; replay++)
            {
                for (var i = 0; i < 60; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
                for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.True(Matches(expected.Outputs["Esnr"][i], point.Value)); foreach (var key in expected.Outputs.Keys) Assert.True(Matches(expected.Outputs[key][i], point.Outputs![key]), $"{key} at {i}"); } }
            }
        }
        finally { ((IDisposable)state).Dispose(); }
        return expected.Outputs;
    }
    [Fact]
    public void WideAndSubnormalEnergyProducesFiniteDecibelRatios()
    {
        foreach (var mode in Enumerable.Range(0, 3)) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) { var actual = Check(Enumerable.Range(0, 90).Select(i => Candle(Math.Sin(i * .27) * scale, scale, -scale)).ToArray(), mode); Assert.All(actual["Esnr"], value => Assert.False(double.IsNaN(value) || double.IsInfinity(value))); }
    }
    [Fact]
    public void StartupZeroRangeAndSignedRangesRetainTheirDistinctConventions()
    {
        foreach (var mode in Enumerable.Range(0, 3))
        {
            Check(Array.Empty<Bar>(), mode); var zero = Check(Enumerable.Repeat(Candle(0, 0, 0), 75).ToArray(), mode); Assert.Equal(mode == 0 ? 1.5 : 0, zero["Esnr"][0]);
            var noEnergy = Check(Enumerable.Repeat(Candle(0, 1, -1), 75).ToArray(), mode); Assert.Equal(mode == 2 ? 0 : 1.5, noEnergy["Esnr"][0]);
            Check(Enumerable.Range(0, 75).Select(i => Candle(Math.Sin(i * .21), -2, 2)).ToArray(), mode);
            foreach (var length in new[] { int.MinValue, 0, 1, 31, int.MaxValue }) Check(Enumerable.Range(0, 45).Select(i => Candle(Math.Sin(i * .21))).ToArray(), mode, length);
        }
    }
    [Fact]
    public void ExactPowerOfTwoScalingPreservesMamaEnergyRatios()
    {
        foreach (var mode in new[] { 0, 1 })
        {
            var prices = Enumerable.Range(0, 95).Select(i => (double)(i % 9 - 4)).ToArray(); var normal = Check(prices.Select(v => Candle(v, 5, -5)).ToArray(), mode);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) Equal(normal["Esnr"], Check(prices.Select(v => Candle(v * scale, 5 * scale, -5 * scale)).ToArray(), mode)["Esnr"]);
        }
    }
    [Fact]
    public void ExtremeEnergyToNoiseExponentsNeverFormIntermediateInfinity()
    {
        foreach (var mode in Enumerable.Range(0, 3)) foreach (var pair in new[] { (Math.Pow(2, 1000), double.Epsilon), (Math.Pow(2, -1000), double.MaxValue) })
        { var actual = Check(Enumerable.Range(0, 95).Select(i => Candle(Math.Sin(i * .21) * pair.Item1, pair.Item2, -pair.Item2)).ToArray(), mode); Assert.All(actual["Esnr"], value => Assert.False(double.IsNaN(value) || double.IsInfinity(value))); }
    }
    [Fact]
    public void SelectedPricesPreserveOriginalNoiseRanges()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100, 7, -2)).ToArray(); var projected = selected.Select(v => Candle(v, 7, -2)).ToArray();
        foreach (var mode in Enumerable.Range(0, 3)) { var expected = BuiltInFormulaReferences.MamaNoiseValues(projected, 6, mode); var data = Data(bars); data.SetCustomValues(selected); Batch(data, mode, 6); Assert.Equal(expected.Signals, data.SignalsList); foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeMamaNoiseFast(source, context, 6, mode, key); Equal(expected.Outputs[key], result.ToArray()); if (mode == 1) { using var legacy = IndicatorCompute.ComputeEhlersSignalToNoiseRatioV2Fast(source, context); Equal(expected.Outputs[key], legacy.ToArray()); } } }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEnergyOrNoiseHistory()
    {
        foreach (var mode in Enumerable.Range(0, 3)) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(mode, 6); var control = State(mode, 6);
            try
            {
                for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
                var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
                for (var i = 0; i < 15; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
            }
            finally { ((IDisposable)state).Dispose(); ((IDisposable)control).Dispose(); }
        }
    }
}
