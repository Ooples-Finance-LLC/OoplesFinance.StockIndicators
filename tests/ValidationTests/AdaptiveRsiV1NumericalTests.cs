using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveRsiV1NumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close) => new(DateTime.UnixEpoch, 0, 4, -4, close, 1);
    private static IStreamingIndicatorState State(bool fisher, double fraction = .5) => fisher ? new EhlersAdaptiveRsiFisherTransformV1State() : new EhlersAdaptiveRelativeStrengthIndexV1State(fraction);
    private static StockData Batch(StockData data, bool fisher, double fraction = .5) => fisher ? data.CalculateEhlersAdaptiveRsiFisherTransformV1() : data.CalculateEhlersAdaptiveRelativeStrengthIndexV1(fraction);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveRelativeStrengthIndexV1) || c.IndicatorType == typeof(EhlersAdaptiveRsiFisherTransformV1)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentTravelRatios(IndicatorValidationCase c, string route)
    {
        var indicator = c.Factory(); var options = ((IBuiltInIndicator)indicator).CreateOptions(); var fraction = options is EhlersAdaptiveRelativeStrengthIndexV1SpecOptions rsi ? rsi.CycPart : .5;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AdaptiveRsiV1Values(bars, fraction, indicator is EhlersAdaptiveRsiFisherTransformV1).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, bool fisher = false, double fraction = .5)
    {
        var expected = BuiltInFormulaReferences.AdaptiveRsiV1Values(bars, fraction, fisher); var batch = Batch(Data(bars), fisher, fraction); Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); var primary = fisher ? "Earsift" : "Earsi"; Assert.Equal(expected.Outputs[primary], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var result = IndicatorCompute.ComputeAdaptiveRsiV1Fast(Data(bars), context, fraction, fisher, key); Assert.Equal(expected.Outputs[key], result.ToArray()); }
        var state = State(fisher, fraction);
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
    public void WideAndSubnormalTravelPreservesRatiosSignalsAndPreview()
    {
        foreach (var fisher in new[] { false, true }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) Check(Enumerable.Range(0, 90).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray(), fisher);
    }
    [Fact]
    public void ExtremeCycleFractionsUseAvailableHistoryWithoutEagerAllocation()
    {
        var bars = Enumerable.Range(0, 160).Select(i => Candle(Math.Sin(i * .19))).ToArray(); foreach (var fraction in new[] { -double.MaxValue, -1, 0, double.Epsilon, .25, 1, 3, double.MaxValue }) Check(bars, fraction: fraction);
        Assert.All(Check(bars, fraction: 0)["Earsi"], value => Assert.Equal(0, value));
        foreach (var invalid in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity }) Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveRelativeStrengthIndexV1State(invalid));
    }
    [Fact]
    public void StartupSilenceAndPowerOfTwoScalingPreserveBoundedOutputs()
    {
        foreach (var fisher in new[] { false, true }) { Check(Array.Empty<Bar>(), fisher); Check(Enumerable.Repeat(Candle(0), 75).ToArray(), fisher); }
        var first = Check(new[] { Candle(1) }); Assert.Equal(100, first["Earsi"][0]); Assert.Equal(99, first["Signal"][0]);
        var prices = Enumerable.Range(0, 95).Select(i => (double)(i % 9 - 4)).ToArray(); var normal = Check(prices.Select(Candle).ToArray()); foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) { var actual = Check(prices.Select(p => Candle(p * scale)).ToArray()); foreach (var key in normal.Keys) Assert.Equal(normal[key], actual[key]); }
    }
    [Fact]
    public void SelectedPricesReachBatchAndEveryFastOutput()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var fisher in new[] { false, true }) { var expected = BuiltInFormulaReferences.AdaptiveRsiV1Values(selected.Select(Candle).ToArray(), fisher: fisher); var data = Data(bars); data.SetCustomValues(selected); Batch(data, fisher); Assert.Equal(expected.Signals, data.SignalsList); foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeAdaptiveRsiV1Fast(source, context, .5, fisher, key); Assert.Equal(expected.Outputs[key], result.ToArray()); } }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAdaptiveOrTravelHistory()
    {
        foreach (var fisher in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(fisher); var control = State(fisher);
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
