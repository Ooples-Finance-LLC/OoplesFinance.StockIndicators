using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HpLpRoofingNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersHpLpRoofingFilter) || c.IndicatorType == typeof(EhlersZeroMeanRoofingFilter)).Select(c => new object[] { c });
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
    {
        var o = ((IBuiltInIndicator)indicator).CreateOptions();
        if (o is EhlersHpLpRoofingFilterSpecOptions roof) return new() { ["Ehplprf"] = BuiltInFormulaReferences.HpLpRoofingOutputs(bars, roof.Length1, roof.Length2).Roof };
        var zero = (EhlersZeroMeanRoofingFilterSpecOptions)o; return new() { ["Ezmrf"] = BuiltInFormulaReferences.HpLpRoofingOutputs(bars, zero.Length1, zero.Length2).Zero };
    }
    private static void Check(Bar[] bars, int length1 = 48, int length2 = 10)
    {
        var expected = BuiltInFormulaReferences.HpLpRoofingOutputs(bars, length1, length2);
        Assert.Equal(expected.Roof, Data(bars).CalculateEhlersHpLpRoofingFilter(length1, length2).OutputValues["Ehplprf"]);
        Assert.Equal(expected.Zero, Data(bars).CalculateEhlersZeroMeanRoofingFilter(length1, length2).OutputValues["Ezmrf"]);
        using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeEhlersHpLpRoofingFilterFast(Data(bars), context, length1, length2); Assert.Equal(expected.Roof, raw.ToArray());
        using var zeroRaw = IndicatorCompute.ComputeEhlersZeroMeanRoofingFilterFast(Data(bars), context, length1, length2); Assert.Equal(expected.Zero, zeroRaw.ToArray());
        var core = new double[bars.Length]; var close = bars.Select(b => b.Close).ToArray(); OscillatorCore.EhlersHpLpRoofingFilter(close, core, length1, length2); Assert.Equal(expected.Roof, core); OscillatorCore.EhlersZeroMeanRoofingFilter(close, core, length1, length2); Assert.Equal(expected.Zero, core);
        var roof = new EhlersHpLpRoofingFilterState(length1, length2); var zero = new EhlersZeroMeanRoofingFilterState(length1, length2);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) { roof.Update(Native(b), true, false); zero.Update(Native(b), true, false); } roof.Reset(); zero.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                roof.Update(Native(Candle(double.MaxValue)), false, false); zero.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var first = roof.Update(Native(bars[i]), final, true); var second = zero.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected.Roof[i], first.Value); Assert.Equal(expected.Roof[i], first.Outputs!["Ehplprf"]); Assert.Equal(expected.Zero[i], second.Value); Assert.Equal(expected.Zero[i], second.Outputs!["Ezmrf"]);
                }
            }
        }
    }
    [Fact]
    public void HandFirstDifferenceAndSecondBarImpulse()
    {
        var bars = new[] { 0d, 2 }.Select(v => Candle(v)).ToArray(); var expected = BuiltInFormulaReferences.HpLpRoofingOutputs(bars, 1, 1);
        var alpha = (Math.Cos(.99) + Math.Sin(.99) - 1) / Math.Cos(.99); var gain = 1 - alpha / 2;
        var decay = Math.Exp(-Math.Sqrt(2) * Math.PI); var c2 = 2 * decay * Math.Cos(.99); var c3 = -decay * decay; var roof = (1 - c2 - c3) * gain;
        Assert.Equal(new[] { 0d, roof }, expected.Roof); Assert.Equal(new[] { 0d, roof * gain }, expected.Zero); Check(bars, 1, 1);
        var constant = Enumerable.Repeat(Candle(3), 40).ToArray(); Assert.All(BuiltInFormulaReferences.HpLpRoofingOutputs(constant).Roof, v => Assert.Equal(0, v)); Check(constant); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void IndependentPeriodsAndCoefficientClampBoundaries()
    {
        var bars = Enumerable.Range(0, 40).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var length1 in new[] { 0, 1, 6, 7, 48, int.MaxValue }) foreach (var length2 in new[] { 0, 1, 4, 5, 10, int.MaxValue }) Check(bars, length1, length2);
        Assert.Throws<ArgumentException>(() => OscillatorCore.EhlersHpLpRoofingFilter(new[] { 1d }, Array.Empty<double>())); Assert.Throws<ArgumentException>(() => OscillatorCore.EhlersZeroMeanRoofingFilter(new[] { 1d }, Array.Empty<double>()));
    }
    [Fact]
    public void ExtendedDifferencesAndComposedStagesRecover()
    {
        var extreme = Enumerable.Repeat(double.MaxValue, 10).Concat(Enumerable.Repeat(-double.MaxValue, 10)).Concat(new[] { double.Epsilon, -double.Epsilon, 0d, 2, 1, 2, 1 }).Select(v => Candle(v)).ToArray();
        foreach (var length in new[] { 1, 5, 48, int.MaxValue }) Check(extreme, length, 10);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 50).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 7, 5);
        var ordinary = Enumerable.Range(0, 40).Select(i => Candle(i % 7 - 3)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        var first = BuiltInFormulaReferences.HpLpRoofingOutputs(ordinary); var second = BuiltInFormulaReferences.HpLpRoofingOutputs(scaled);
        Assert.Equal(first.Roof.Select(v => v * Math.ScaleB(1, 600)), second.Roof); Assert.Equal(first.Zero.Select(v => v * Math.ScaleB(1, 600)), second.Zero);
    }
    [Fact]
    public void SelectedPricesDriveBothFilters()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray(); var selectedBars = bars.Select((b, i) => Candle(selected[i])).ToArray(); var expected = BuiltInFormulaReferences.HpLpRoofingOutputs(selectedBars);
        using var context = new ComputeContext();
        foreach (var zero in new[] { false, true })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); var spec = zero ? new IndicatorSpec(IndicatorName.EhlersZeroMeanRoofingFilter, new EhlersZeroMeanRoofingFilterSpecOptions(48, 10)) : new IndicatorSpec(IndicatorName.EhlersHpLpRoofingFilter, new EhlersHpLpRoofingFilterSpecOptions(48, 10));
            using var arm = IndicatorCompute.TryComputeFast(data, spec, context); Assert.NotNull(arm); Assert.Equal(zero ? expected.Zero : expected.Roof, arm.Value.ToArray());
            var batch = Data(bars); batch.SetCustomValues(selected.ToList()); if (zero) batch.CalculateEhlersZeroMeanRoofingFilter(); else batch.CalculateEhlersHpLpRoofingFilter(); Assert.Equal(zero ? expected.Zero : expected.Roof, batch.OutputValues[zero ? "Ezmrf" : "Ehplprf"]);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceAnyStage()
    {
        foreach (var variant in new[] { 1, 2 }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant == 1 ? new EhlersHpLpRoofingFilterState() : new EhlersZeroMeanRoofingFilterState();
            var state = Create(); var control = Create(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 20).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
