using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MultiLevelNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, close, high, low, 0, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MultiLevelIndicator)).Select(c => new object[] { c });
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
    { var o = (MultiLevelIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Mli"] = BuiltInFormulaReferences.MultiLevelOutputs(bars, o.Length, o.Factor) }; }
    private static void Check(Bar[] bars, int length = 14, double factor = 10000)
    {
        var expected = BuiltInFormulaReferences.MultiLevelOutputs(bars, length, factor);
        Assert.Equal(expected, Data(bars).CalculateMultiLevelIndicator(length, factor).OutputValues["Mli"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeMultiLevelIndicatorFast(Data(bars), context, length, factor); Assert.Equal(expected, raw.ToArray());
        using var state = new MultiLevelIndicatorState(length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Mli"]); }
            }
        }
    }
    [Fact]
    public void HandLaggedOpenStartupAndScale()
    {
        var bars = new[] { 1d, 3, 4, 2, 0 }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { -2d, -6, -6, 2, 8 }, BuiltInFormulaReferences.MultiLevelOutputs(bars, 2, 2)); Check(bars, 2, 2);
        foreach (var length in new[] { 0, 1, 2, int.MaxValue }) Check(bars, length); Check(Array.Empty<Bar>());
        foreach (var factor in new[] { 0d, -2, .25, double.Epsilon, double.MaxValue }) Check(bars, 2, factor);
    }
    [Fact]
    public void OverflowingDifferenceCanHaveFiniteScaledOutput()
    {
        var bars = new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, double.Epsilon, -double.Epsilon, 2, 1 }.Select(v => Candle(v)).ToArray();
        Assert.Equal(double.MaxValue / 2, BuiltInFormulaReferences.MultiLevelOutputs(bars, 1, .25)[1]);
        foreach (var factor in new[] { 0d, .25, -.25, 1, double.Epsilon, double.MaxValue }) Check(bars, 1, factor);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 60).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 3, .25);
    }
    [Fact]
    public void SelectedPricesPreserveOriginalOpenHistory()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(i % 7 - 3)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var expected = BuiltInFormulaReferences.MultiLevelOutputs(bars); Assert.Contains(expected, v => v != 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeMultiLevelIndicatorFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.MultiLevelIndicator, new MultiLevelIndicatorSpecOptions(14)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateMultiLevelIndicator().OutputValues["Mli"]);
    }
    [Fact]
    public void NonfiniteFactorsAreRejectedBeforeComputation()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MultiLevelIndicatorState(2, factor));
            var data = Data(new[] { Candle(1), Candle(2) }); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateMultiLevelIndicator(2, factor));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => { using var raw = IndicatorCompute.ComputeMultiLevelIndicatorFast(data, context, 2, factor); });
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceOpenHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new MultiLevelIndicatorState(); using var control = new MultiLevelIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
