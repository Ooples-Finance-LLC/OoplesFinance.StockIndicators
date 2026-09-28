using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class GeneralFilterNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(GeneralFilterEstimator)).Select(c => new object[] { c });
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
    { var o = (GeneralFilterEstimatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Gfe"] = BuiltInFormulaReferences.GeneralFilterOutputs(bars, o.Length) }; }
    private static void Check(Bar[] bars, int length = 100, double beta = 5.25, double gamma = 1, double zeta = 1)
    {
        var expected = BuiltInFormulaReferences.GeneralFilterOutputs(bars, length, beta, gamma, zeta);
        Assert.Equal(expected, Data(bars).CalculateGeneralFilterEstimator(length, beta, gamma, zeta).OutputValues["Gfe"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeGeneralFilterEstimatorFast(Data(bars), context, length, beta, gamma, zeta); Assert.Equal(expected, raw.ToArray());
        using var state = new GeneralFilterEstimatorState(length, beta, gamma, zeta);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Gfe"]); }
            }
        }
    }
    [Fact]
    public void HandLaggedInnerAndOuterCorrections()
    {
        var bars = new[] { 1d, 3, 2, 0, 0 }.Select(v => Candle(v)).ToArray();
        Assert.Equal(new[] { 1d, 1, 1.375, 1.125, .46875 }, BuiltInFormulaReferences.GeneralFilterOutputs(bars, 2, 1, 1, .5)); Check(bars, 2, 1, 1, .5);
        foreach (var length in new[] { 0, 1, 2, int.MaxValue }) Check(bars, length); Check(Array.Empty<Bar>());
        foreach (var beta in new[] { .25, 1d, 2.5, 100, double.MaxValue }) foreach (var gamma in new[] { 0d, -1, .5, 2 }) foreach (var zeta in new[] { 0d, .25, 1, -2, 2 }) Check(bars, 3, beta, gamma, zeta);
    }
    [Fact]
    public void ExtendedRecurrencesAndSubnormalStages()
    {
        var extreme = new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, -double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0, 2, 1, 2, 1 }.Select(v => Candle(v)).ToArray();
        foreach (var zeta in new[] { 0d, .5, 1, 2, -2, double.MaxValue }) Check(extreme, 3, 2, .5, zeta);
        foreach (var gamma in new[] { double.MaxValue, -double.MaxValue }) Check(extreme, 3, 2, gamma, .5);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 45).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 3, 2, .5, .25);
        var ordinary = new[] { 1d, 2, 3, 1, 2, 4, 1 }.Select(v => Candle(v)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        Assert.Equal(BuiltInFormulaReferences.GeneralFilterOutputs(ordinary, 3, 2, .5, .25).Select(v => v * Math.ScaleB(1, 600)), BuiltInFormulaReferences.GeneralFilterOutputs(scaled, 3, 2, .5, .25));
    }
    [Fact]
    public void SelectedPricesDriveBothRecurrences()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var selectedBars = bars.Select((b, i) => Candle(selected[i])).ToArray(); var expected = BuiltInFormulaReferences.GeneralFilterOutputs(selectedBars); Assert.Contains(expected, v => v != 2);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeGeneralFilterEstimatorFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.GeneralFilterEstimator, new GeneralFilterEstimatorSpecOptions(100)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateGeneralFilterEstimator().OutputValues["Gfe"]);
    }
    [Fact]
    public void InvalidParametersAreRejectedBeforeComputation()
    {
        void Reject(double beta, double gamma, double zeta)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeneralFilterEstimatorState(2, beta, gamma, zeta));
            var data = Data(new[] { Candle(1), Candle(2) }); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateGeneralFilterEstimator(2, beta, gamma, zeta));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => { using var raw = IndicatorCompute.ComputeGeneralFilterEstimatorFast(data, context, 2, beta, gamma, zeta); });
        }
        foreach (var beta in new[] { 0d, -1, double.Epsilon, double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Reject(beta, 1, 1);
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) { Reject(1, value, 1); Reject(1, 1, value); }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceRecurrences()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new GeneralFilterEstimatorState(); using var control = new GeneralFilterEstimatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 30).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
