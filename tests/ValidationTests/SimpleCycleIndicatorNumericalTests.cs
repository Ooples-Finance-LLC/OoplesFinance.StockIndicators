using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SimpleCycleIndicatorNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSimpleCycleIndicator)).Select(c => new object[] { c });
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
    { var o = (EhlersSimpleCycleIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Esci"] = BuiltInFormulaReferences.SimpleCycleIndicatorOutputs(bars, o.Alpha) }; }
    private static void Check(Bar[] bars, double alpha = .07)
    {
        var expected = BuiltInFormulaReferences.SimpleCycleIndicatorOutputs(bars, alpha);
        Assert.Equal(expected, Data(bars).CalculateEhlersSimpleCycleIndicator(alpha).OutputValues["Esci"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEhlersSimpleCycleIndicatorFast(Data(bars), context, alpha); Assert.Equal(expected, raw.ToArray());
        var core = new double[bars.Length]; OscillatorCore.EhlersSimpleCycleIndicator(bars.Select(b => b.Close).ToArray(), core, alpha); Assert.Equal(expected, core);
        var state = new EhlersSimpleCycleIndicatorState(alpha);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Esci"]); }
            }
        }
    }
    [Fact]
    public void HandSevenBarStartupThenInternalRecurrence()
    {
        var bars = new[] { 1d, 3, 2, 0, 0, 2, 1, 4, 2 }.Select(v => Candle(v)).ToArray(); var expected = BuiltInFormulaReferences.SimpleCycleIndicatorOutputs(bars, 1);
        Assert.Equal(new[] { .25, .25, -.75, -.25, .5, .5, -.75 }, expected.Take(7)); Assert.Equal(((10d / 6) - (2 * (5d / 6)) + (4d / 6)) * .25, expected[7]); Check(bars, 1);
        foreach (var alpha in new[] { -2d, -1, 0, .07, .5, 1, 2 }) Check(bars, alpha); Check(Array.Empty<Bar>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.EhlersSimpleCycleIndicator(new[] { 1d }, Array.Empty<double>()));
    }
    [Fact]
    public void ExtendedSmoothingAndHiddenStartupHistoryRecover()
    {
        var extreme = Enumerable.Repeat(double.MaxValue, 10).Concat(Enumerable.Repeat(-double.MaxValue, 10)).Concat(new[] { double.Epsilon, -double.Epsilon, 0d, 2, 1, 2, 1 }).Select(v => Candle(v)).ToArray();
        foreach (var alpha in new[] { .07, 0d, 1, 2, -2, double.MaxValue, -double.MaxValue }) Check(extreme, alpha);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 50).Select(i => Candle((i % 7 - 3) * scale)).ToArray());
        var ordinary = Enumerable.Range(0, 25).Select(i => Candle(i % 5 - 2)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        Assert.Equal(BuiltInFormulaReferences.SimpleCycleIndicatorOutputs(ordinary).Select(v => v * Math.ScaleB(1, 600)), BuiltInFormulaReferences.SimpleCycleIndicatorOutputs(scaled));
    }
    [Fact]
    public void SelectedPricesDriveEveryRecurrenceTerm()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var selectedBars = bars.Select((b, i) => Candle(selected[i])).ToArray(); var expected = BuiltInFormulaReferences.SimpleCycleIndicatorOutputs(selectedBars); Assert.Contains(expected, v => v != 2);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeEhlersSimpleCycleIndicatorFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersSimpleCycleIndicator, new EhlersSimpleCycleIndicatorSpecOptions(.07)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateEhlersSimpleCycleIndicator().OutputValues["Esci"]);
    }
    [Fact]
    public void NonfiniteAlphaIsRejectedBeforeComputation()
    {
        foreach (var beta in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersSimpleCycleIndicatorState(beta));
            var data = Data(new[] { Candle(1), Candle(2) }); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateEhlersSimpleCycleIndicator(beta));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => { using var raw = IndicatorCompute.ComputeEhlersSimpleCycleIndicatorFast(data, context, beta); });
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvancePriceOrPhaseHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersSimpleCycleIndicatorState(); var control = new EhlersSimpleCycleIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
