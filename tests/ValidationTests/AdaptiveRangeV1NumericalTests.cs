using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveRangeV1NumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = -4) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    private static IStreamingIndicatorState State(bool commodity, double fraction, double constant) => commodity ? new EhlersAdaptiveCommodityChannelIndexV1State(fraction, constant) : new EhlersAdaptiveStochasticIndicatorV1State(fraction);
    private static StockData Batch(StockData data, bool commodity, double fraction, double constant) => commodity ? data.CalculateEhlersAdaptiveCommodityChannelIndexV1(fraction, constant) : data.CalculateEhlersAdaptiveStochasticIndicatorV1(fraction);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveStochasticIndicatorV1) || c.IndicatorType == typeof(EhlersAdaptiveCommodityChannelIndexV1)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeAndDeviation(IndicatorValidationCase c, string route)
    {
        var indicator = c.Factory(); var options = ((IBuiltInIndicator)indicator).CreateOptions(); var commodity = options as EhlersAdaptiveCommodityChannelIndexV1SpecOptions; var fraction = commodity?.CycPart ?? ((EhlersAdaptiveStochasticIndicatorV1SpecOptions)options).CycPart;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AdaptiveRangeV1Values(bars, fraction, commodity != null, commodity?.Constant ?? .015).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, bool commodity, double fraction, double constant = .015)
    {
        var expected = BuiltInFormulaReferences.AdaptiveRangeV1Values(bars, fraction, commodity, constant); var batch = Batch(Data(bars), commodity, fraction, constant); Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); var primary = commodity ? "Eacci" : "Easi"; Assert.Equal(expected.Outputs[primary], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var result = IndicatorCompute.ComputeAdaptiveRangeV1Fast(Data(bars), context, fraction, commodity, constant, key); Assert.Equal(expected.Outputs[key], result.ToArray()); }
        var state = State(commodity, fraction, constant);
        try
        {
            for (var replay = 0; replay < 2; replay++)
            {
                for (var i = 0; i < 60; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
                for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Outputs[primary][i], point.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
            }
        }
        finally { ((IDisposable)state).Dispose(); }
        return expected.Outputs;
    }
    [Fact]
    public void WideAndSubnormalRangesPreserveEveryOutputAndSignal()
    {
        foreach (var commodity in new[] { false, true }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) Check(Enumerable.Range(0, 90).Select(i => Candle(Math.Sin(i * .27) * scale, scale, -scale)).ToArray(), commodity, commodity ? 1 : .5);
        Check(Enumerable.Range(0, 90).Select(i => Candle(Math.Sin(i * .27), 5 + Math.Sin(i * .17), -2 - Math.Cos(i * .11))).ToArray(), true, 1);
        Check(Enumerable.Range(0, 90).Select(i => Candle(Math.Sin(i * .27), i % 13 == 5 ? 7 : 2, i % 17 == 6 ? -8 : -1)).ToArray(), false, .5);
    }
    [Fact]
    public void HugeFractionsAndStartupPaddingUseLazyHistory()
    {
        var bars = Enumerable.Range(0, 100).Select(i => Candle(2 + Math.Sin(i * .19), 4, 1)).ToArray(); foreach (var commodity in new[] { false, true }) foreach (var fraction in new[] { -double.MaxValue, 0, double.Epsilon, .25, 3, 10, double.MaxValue }) Check(bars, commodity, fraction);
        var first = Check(new[] { Candle(1, 1, 1) }, true, 10); Assert.InRange(first["Eacci"][0], 133, 134);
        var stochastic = Check(new[] { Candle(2, 4, 1) }, false, 10); Assert.Equal(50, stochastic["Easi"][0]);
    }
    [Fact]
    public void PublishedOverflowDoesNotPoisonLaterAverages()
    {
        var bars = Enumerable.Range(0, 120).Select(i => Candle(i == 0 ? double.MaxValue : i % 3, 1, 0)).ToArray(); var stochastic = Check(bars, false, .5); Assert.True(double.IsPositiveInfinity(stochastic["Easi"][0])); Assert.False(double.IsInfinity(stochastic["Signal"][119]));
        foreach (var constant in new[] { .015, -.015, double.Epsilon, double.MaxValue }) Check(Enumerable.Range(0, 80).Select(i => Candle(Math.Sin(i * .3))).ToArray(), true, 3, constant);
    }
    [Fact]
    public void SilenceEmptyInputAndTypicalPriceRemainDefined()
    {
        foreach (var commodity in new[] { false, true }) { Check(Array.Empty<Bar>(), commodity, 1); var zero = Check(Enumerable.Repeat(Candle(0, 0, 0), 75).ToArray(), commodity, 1); foreach (var values in zero.Values) Assert.All(values, value => Assert.Equal(0, value)); }
        var huge = Check(Enumerable.Repeat(Candle(double.MaxValue, double.MaxValue, double.MaxValue), 75).ToArray(), true, 1); Assert.Equal(0, huge["Eacci"][0]); Assert.All(huge["Eacci"], value => Assert.False(double.IsNaN(value) || double.IsInfinity(value)));
    }
    [Fact]
    public void SelectedPricesPreserveHighLowAndOverrideTypicalPrice()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100, 7, -2)).ToArray(); var projected = selected.Select(v => Candle(v, 7, -2)).ToArray();
        foreach (var commodity in new[] { false, true })
        {
            var fraction = commodity ? 1 : .5; var expected = BuiltInFormulaReferences.AdaptiveRangeV1Values(projected, fraction, commodity, selected: true); var data = Data(bars); data.SetCustomValues(selected); Batch(data, commodity, fraction, .015); Assert.Equal(expected.Signals, data.SignalsList);
            foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeAdaptiveRangeV1Fast(source, context, fraction, commodity, .015, key); Assert.Equal(expected.Outputs[key], result.ToArray()); }
            var state = State(commodity, fraction, .015); try { if (state is ICustomInputConsumer custom) custom.ReadCloseAsInput(); for (var i = 0; i < projected.Length; i++) { var point = state.Update(Native(projected[i]), true, true); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } } finally { ((IDisposable)state).Dispose(); }
        }
    }
    [Fact]
    public void InvalidCandlesAndParametersCannotCorruptHistory()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) { Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveStochasticIndicatorV1State(invalid)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveCommodityChannelIndexV1State(invalid)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveCommodityChannelIndexV1State(constant: invalid)); } Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveCommodityChannelIndexV1State(constant: 0));
        foreach (var commodity in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(commodity, 1, .015); var control = State(commodity, 1, .015);
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
