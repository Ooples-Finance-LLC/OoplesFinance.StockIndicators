using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DetrendedLeadingNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersDetrendedLeadingIndicator)).Select(c => new object[] { c });
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
    { var o = (EhlersDetrendedLeadingIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.DetrendedLeadingOutputs(bars, o.Length); }
    private static void Check(Bar[] bars, int length = 14)
    {
        var expected = BuiltInFormulaReferences.DetrendedLeadingOutputs(bars, length); var batch = Data(bars).CalculateEhlersDetrendedLeadingIndicator(length);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeEhlersDetrendedLeadingIndicatorFast(Data(bars), context, length, key == "Dsp"); Assert.Equal(expected[key], raw.ToArray()); }
        var state = new EhlersDetrendedLeadingIndicatorState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(0, double.MaxValue, -double.MaxValue / 2))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(0, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Deli"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void HandTwoBarExtremaAndTwoRateDifference()
    {
        var bars = new[] { 4d, 8, 0, 0 }.Select(h => Candle(0, h, 0)).ToArray(); var expected = BuiltInFormulaReferences.DetrendedLeadingOutputs(bars, 3);
        Assert.Equal(new[] { 0d, .5, .625, -.40625 }, expected["Dsp"]); Assert.Equal(new[] { 0d, .25, .1875, -.421875 }, expected["Deli"]); Check(bars, 3);
        foreach (var length in new[] { 0, 1, 2, int.MaxValue }) Check(bars, length); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void ExtremeMidpointsAndRecurrencesRecover()
    {
        var extreme = new[] { double.MaxValue, double.MaxValue / 2, -double.MaxValue, -double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0, 2, 1, 2, 1 }.Select(v => Candle(0, v, v)).ToArray();
        foreach (var length in new[] { 1, 3, 14, int.MaxValue }) Check(extreme, length);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 60).Select(i => Candle(0, (i % 7 - 2) * scale, (i % 7 - 3) * scale)).ToArray(), 3);
        var ordinary = new[] { 1d, 2, 3, 1, 2, 4, 1 }.Select(v => Candle(0, v, v / 2)).ToArray(); var scaled = ordinary.Select(b => Candle(0, b.High * Math.ScaleB(1, 600), b.Low * Math.ScaleB(1, 600))).ToArray();
        foreach (var key in new[] { "Dsp", "Deli" }) Assert.Equal(BuiltInFormulaReferences.DetrendedLeadingOutputs(ordinary, 3)[key].Select(v => v * Math.ScaleB(1, 600)), BuiltInFormulaReferences.DetrendedLeadingOutputs(scaled, 3)[key]);
    }
    [Fact]
    public void SelectedPricesPreserveOriginalExtrema()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2, i % 7 + 1, i % 3 - 2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var expected = BuiltInFormulaReferences.DetrendedLeadingOutputs(bars); Assert.Contains(expected["Dsp"], v => v != 0);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeEhlersDetrendedLeadingIndicatorFast(direct, context, detrendedPrice: key == "Dsp"); Assert.Equal(expected[key], raw.ToArray());
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersDetrendedLeadingIndicator, new EhlersDetrendedLeadingIndicatorSpecOptions(14), key), context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray());
        }
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateEhlersDetrendedLeadingIndicator(); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceExtremaOrRecurrences()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersDetrendedLeadingIndicatorState(); var control = new EhlersDetrendedLeadingIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
