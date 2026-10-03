using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AverageErrorNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAverageErrorFilter)).Select(c => new object[] { c });
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
    { var o = (EhlersAverageErrorFilterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Eaef"] = BuiltInFormulaReferences.AverageErrorOutputs(bars, o.Length) }; }
    private static void Check(Bar[] bars, int length = 27)
    {
        var expected = BuiltInFormulaReferences.AverageErrorOutputs(bars, length);
        Assert.Equal(expected, Data(bars).CalculateEhlersAverageErrorFilter(length).OutputValues["Eaef"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEhlersAverageErrorFilterFast(Data(bars), context, length); Assert.Equal(expected, raw.ToArray());
        var state = new EhlersAverageErrorFilterState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Eaef"]); }
            }
        }
    }
    [Fact]
    public void ThreeBarsSeedSmootherBeforeErrorCorrection()
    {
        var bars = new[] { 1d, 3, 2, 0, 4, 1, 2, 3 }.Select(v => Candle(v)).ToArray(); var expected = BuiltInFormulaReferences.AverageErrorOutputs(bars, 1);
        Assert.Equal(new[] { 1d, 3, 2 }, expected.Take(3));
        var decay = Math.Exp(-.99); var c2 = 2 * decay * Math.Cos(.99); var c3 = -decay * decay; var c1 = 1 - c2 - c3;
        var smooth = (.5 * c1 * 2) + (c2 * 2) + (c3 * 3); var error = c1 * -smooth; Assert.Equal(smooth + error, expected[3]);
        Check(bars, 1); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void CoefficientClampsAndPeriodNormalizationAgreeAcrossRoutes()
    {
        var bars = Enumerable.Range(0, 60).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 4, 5, 27, 444, 445, int.MaxValue }) Check(bars, length);
        Assert.Equal(BuiltInFormulaReferences.AverageErrorOutputs(bars, 1), BuiltInFormulaReferences.AverageErrorOutputs(bars, 4));
        Assert.Equal(BuiltInFormulaReferences.AverageErrorOutputs(bars, 445), BuiltInFormulaReferences.AverageErrorOutputs(bars, int.MaxValue));
        Assert.NotEqual(BuiltInFormulaReferences.AverageErrorOutputs(bars, 4)[3], BuiltInFormulaReferences.AverageErrorOutputs(bars, 5)[3]);
    }
    [Fact]
    public void ExtendedSmootherAndErrorHistoryRecover()
    {
        var extreme = new[] { double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0, 2, 1, 2, 1 }.Select(v => Candle(v)).ToArray();
        foreach (var length in new[] { 1, 5, 27, int.MaxValue }) Check(extreme, length);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 60).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 5);
        var ordinary = new[] { 1d, 2, 3, 1, 2, 4, 1 }.Select(v => Candle(v)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        Assert.Equal(BuiltInFormulaReferences.AverageErrorOutputs(ordinary, 5).Select(v => v * Math.ScaleB(1, 600)), BuiltInFormulaReferences.AverageErrorOutputs(scaled, 5));
    }
    [Fact]
    public void SelectedPricesDriveSmootherAndError()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var selectedBars = bars.Select((b, i) => Candle(selected[i])).ToArray(); var expected = BuiltInFormulaReferences.AverageErrorOutputs(selectedBars); Assert.Contains(expected, v => v != 2);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeEhlersAverageErrorFilterFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersAverageErrorFilter, new EhlersAverageErrorFilterSpecOptions(27)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateEhlersAverageErrorFilter().OutputValues["Eaef"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceEitherRecurrence()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersAverageErrorFilterState(); var control = new EhlersAverageErrorFilterState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
