using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AllPassPhaseNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAllPassPhaseShifter)).Select(c => new object[] { c });
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
    { var o = (EhlersAllPassPhaseShifterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Eapps"] = BuiltInFormulaReferences.AllPassPhaseOutputs(bars, o.Length) }; }
    private static void Check(Bar[] bars, int length = 20, double beta = .5)
    {
        var expected = BuiltInFormulaReferences.AllPassPhaseOutputs(bars, length, beta);
        Assert.Equal(expected, Data(bars).CalculateEhlersAllPassPhaseShifter(length, beta).OutputValues["Eapps"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEhlersAllPassPhaseShifterFast(Data(bars), context, length, beta); Assert.Equal(expected, raw.ToArray());
        var state = new EhlersAllPassPhaseShifterState(length, beta);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Eapps"]); }
            }
        }
    }
    [Fact]
    public void HandTwoPriceAndTwoPhaseLags()
    {
        var bars = new[] { 1d, 2, 3, 4 }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { .25, -.25, -.5625, -.5 }, BuiltInFormulaReferences.AllPassPhaseOutputs(bars, 1, .5)); Check(bars, 1, .5);
        foreach (var length in new[] { int.MinValue, -2, 0, 1, 2, int.MaxValue }) Check(bars, length); Check(Array.Empty<Bar>());
        foreach (var gain in new[] { 0d, 1, -1, .25, -.5, 2, -2 }) Check(bars, 3, gain);
        Assert.All(BuiltInFormulaReferences.AllPassPhaseOutputs(bars, 1, 0), v => Assert.Equal(0, v));
    }
    [Fact]
    public void ExtendedReciprocalCoefficientsAndPhaseHistoryRecover()
    {
        var extreme = new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, -double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0, 2, 1, 2, 1 }.Select(v => Candle(v)).ToArray();
        foreach (var gain in new[] { .5, 0d, 1, 2, -2, double.MaxValue, -double.MaxValue, double.Epsilon, 1e-200, 1e200 }) Check(extreme, 3, gain);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 50).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 3);
        var ordinary = new[] { 1d, 2, 3, 1, 2, 4, 1 }.Select(v => Candle(v)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        Assert.Equal(BuiltInFormulaReferences.AllPassPhaseOutputs(ordinary, 3).Select(v => v * Math.ScaleB(1, 600)), BuiltInFormulaReferences.AllPassPhaseOutputs(scaled, 3));
    }
    [Fact]
    public void SelectedPricesDriveEveryRecurrenceTerm()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var selectedBars = bars.Select((b, i) => Candle(selected[i])).ToArray(); var expected = BuiltInFormulaReferences.AllPassPhaseOutputs(selectedBars); Assert.Contains(expected, v => v != 2);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeEhlersAllPassPhaseShifterFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersAllPassPhaseShifter, new EhlersAllPassPhaseShifterSpecOptions(20)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateEhlersAllPassPhaseShifter().OutputValues["Eapps"]);
    }
    [Fact]
    public void NonfiniteGainIsRejectedBeforeComputation()
    {
        foreach (var beta in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAllPassPhaseShifterState(2, beta));
            var data = Data(new[] { Candle(1), Candle(2) }); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateEhlersAllPassPhaseShifter(2, beta));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => { using var raw = IndicatorCompute.ComputeEhlersAllPassPhaseShifterFast(data, context, 2, beta); });
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvancePriceOrPhaseHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersAllPassPhaseShifterState(); var control = new EhlersAllPassPhaseShifterState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
