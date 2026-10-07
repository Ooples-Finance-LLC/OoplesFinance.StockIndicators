using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ImpulseReactionNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersImpulseReaction)).Select(c => new object[] { c });
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
    { var o = (EhlersImpulseReactionSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Eir"] = BuiltInFormulaReferences.ImpulseReactionOutputs(bars, o.Length1, o.Length2, o.Q) }; }
    private static void Check(Bar[] bars, int length1 = 2, int length2 = 20, double q = .9)
    {
        var expected = BuiltInFormulaReferences.ImpulseReactionOutputs(bars, length1, length2, q);
        Assert.Equal(expected, Data(bars).CalculateEhlersImpulseReaction(length1, length2, q).OutputValues["Eir"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEhlersImpulseReactionFast(Data(bars), context, length1, length2, q); Assert.Equal(expected, raw.ToArray());
        var core = new double[bars.Length]; OscillatorCore.EhlersImpulseReaction(bars.Select(b => b.Close).ToArray(), core, length1, length2, q); Assert.Equal(expected, core);
        using var state = new EhlersImpulseReactionState(length1, length2, q);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Eir"]); }
            }
        }
    }
    [Fact]
    public void HandDelayedImpulseAndZeroDenominator()
    {
        var bars = new[] { 1d, 3, 2, 1, 0 }.Select(v => Candle(v)).ToArray();
        Assert.Equal(new[] { 50d, 50, 25, -100, 0 }, BuiltInFormulaReferences.ImpulseReactionOutputs(bars, 2, 20, 0)); Check(bars, 2, 20, 0);
        foreach (var length1 in new[] { 0, 1, 3, int.MaxValue }) foreach (var length2 in new[] { 0, 1, 3, int.MaxValue }) Check(bars, length1, length2); Check(Array.Empty<Bar>());
        foreach (var q in new[] { -2d, -1, -.5, 0, .5, 1, 2 }) Check(bars, 2, 3, q);
        Assert.Throws<ArgumentException>(() => OscillatorCore.EhlersImpulseReaction(new[] { 1d }, Array.Empty<double>()));
    }
    [Fact]
    public void ExtendedCoefficientsRecurrenceAndPercentRatio()
    {
        var extreme = new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, -double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0, 2, 1, 2, 1 }.Select(v => Candle(v)).ToArray();
        foreach (var q in new[] { 0d, .9, 1, 2, -2, double.MaxValue, -double.MaxValue }) Check(extreme, 2, 3, q);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 35).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 3, 5);
        var ordinary = new[] { 1d, 2, 3, 1, 2, 4, 1 }.Select(v => Candle(v)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        Assert.Equal(BuiltInFormulaReferences.ImpulseReactionOutputs(ordinary, 3, 5), BuiltInFormulaReferences.ImpulseReactionOutputs(scaled, 3, 5));
        Check(Enumerable.Repeat(Candle(double.MaxValue), 8).ToArray(), 1, 2, 0);
    }
    [Fact]
    public void SelectedPricesDriveLagRecurrenceAndDenominator()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var selectedBars = bars.Select((b, i) => Candle(selected[i])).ToArray(); var expected = BuiltInFormulaReferences.ImpulseReactionOutputs(selectedBars); Assert.Contains(expected, v => v != 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeEhlersImpulseReactionFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersImpulseReaction, new EhlersImpulseReactionSpecOptions(2, 20)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateEhlersImpulseReaction().OutputValues["Eir"]);
    }
    [Fact]
    public void NonfiniteGainIsRejectedBeforeComputation()
    {
        foreach (var q in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersImpulseReactionState(2, 3, q));
            var data = Data(new[] { Candle(1), Candle(2) }); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateEhlersImpulseReaction(2, 3, q));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => { using var raw = IndicatorCompute.ComputeEhlersImpulseReactionFast(data, context, 2, 3, q); });
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.EhlersImpulseReaction(new[] { 1d }, new double[1], 2, 3, q));
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceRecurrences()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersImpulseReactionState(); using var control = new EhlersImpulseReactionState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 20).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
