using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ReverseEmaNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersReverseEmaIndicatorV1) || c.IndicatorType == typeof(EhlersReverseEmaIndicatorV2)).Select(c => new object[] { c });
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
        return o is EhlersReverseEmaIndicatorV1SpecOptions v1 ? new() { ["Erema"] = BuiltInFormulaReferences.ReverseEmaOutputs(bars, v1.Alpha) }
            : new() { ["EremaCycle"] = BuiltInFormulaReferences.ReverseEmaOutputs(bars, ((EhlersReverseEmaIndicatorV2SpecOptions)o).CycleAlpha), ["EremaTrend"] = BuiltInFormulaReferences.ReverseEmaOutputs(bars, ((EhlersReverseEmaIndicatorV2SpecOptions)o).TrendAlpha) };
    }
    private static void Check(Bar[] bars, double alpha = .1, double cycleAlpha = .3)
    {
        var expected = BuiltInFormulaReferences.ReverseEmaOutputs(bars, alpha); var cycle = BuiltInFormulaReferences.ReverseEmaOutputs(bars, cycleAlpha);
        Assert.Equal(expected, Data(bars).CalculateEhlersReverseExponentialMovingAverageIndicatorV1(alpha).OutputValues["Erema"]);
        var batch = Data(bars).CalculateEhlersReverseExponentialMovingAverageIndicatorV2(alpha, cycleAlpha); Assert.Equal(expected, batch.OutputValues["EremaTrend"]); Assert.Equal(cycle, batch.OutputValues["EremaCycle"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeEhlersReverseEmaIndicatorV1Fast(Data(bars), context, alpha); Assert.Equal(expected, raw.ToArray());
        using var trendRaw = IndicatorCompute.ComputeEhlersReverseEmaIndicatorV2Fast(Data(bars), context, alpha, cycleAlpha, IndicatorCompute.EhlersReverseEmaWave.Trend); Assert.Equal(expected, trendRaw.ToArray());
        using var cycleRaw = IndicatorCompute.ComputeEhlersReverseEmaIndicatorV2Fast(Data(bars), context, alpha, cycleAlpha); Assert.Equal(cycle, cycleRaw.ToArray());
        var core = new double[bars.Length]; OscillatorCore.EhlersReverseEmaIndicatorV1(bars.Select(b => b.Close).ToArray(), core, alpha); Assert.Equal(expected, core);
        var state = new EhlersReverseExponentialMovingAverageIndicatorV1State(alpha); var pair = new EhlersReverseExponentialMovingAverageIndicatorV2State(alpha, cycleAlpha);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) { state.Update(Native(b), true, false); pair.Update(Native(b), true, false); } state.Reset(); pair.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false); pair.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true); var both = pair.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Erema"]); Assert.Equal(expected[i], both.Outputs!["EremaTrend"]); Assert.Equal(cycle[i], both.Outputs["EremaCycle"]); Assert.Equal(cycle[i], both.Value);
                }
            }
        }
    }
    [Fact]
    public void HandZeroSeedAndAlphaClamp()
    {
        var bars = new[] { 0d, 1, 1 }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { 0d, .5, .75 }, BuiltInFormulaReferences.ReverseEmaOutputs(bars, .5)); Check(bars, .5);
        foreach (var alpha in new[] { double.NegativeInfinity, -10d, 0, .01, .1, .5, .99, 1, 10, double.PositiveInfinity }) Check(Enumerable.Range(0, 40).Select(i => Candle(i % 5 - 2)).ToArray(), alpha, 1 - alpha);
        Check(Array.Empty<Bar>()); Assert.Throws<ArgumentException>(() => OscillatorCore.EhlersReverseEmaIndicatorV1(new[] { 1d }, Array.Empty<double>()));
    }
    [Fact]
    public void ExtendedReverseStagesPreserveCancellationAndRecovery()
    {
        var extreme = Enumerable.Repeat(double.MaxValue, 20).Concat(Enumerable.Repeat(-double.MaxValue, 20)).Concat(new[] { double.Epsilon, -double.Epsilon, 0d, 2, 1, 2, 1 }).Select(v => Candle(v)).ToArray();
        foreach (var alpha in new[] { .01, .1, .3, .5, .99 }) Check(extreme, alpha, 1 - alpha);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 55).Select(i => Candle((i % 7 - 3) * scale)).ToArray());
        var ordinary = Enumerable.Range(0, 40).Select(i => Candle(i % 7 - 3)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        Assert.Equal(BuiltInFormulaReferences.ReverseEmaOutputs(ordinary).Select(v => v * Math.ScaleB(1, 600)), BuiltInFormulaReferences.ReverseEmaOutputs(scaled));
    }
    [Fact]
    public void BothWavesUseSelectedOriginalPrices()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray(); var selectedBars = bars.Select((b, i) => Candle(selected[i])).ToArray();
        var expected = BuiltInFormulaReferences.ReverseEmaOutputs(selectedBars, .05); var cycle = BuiltInFormulaReferences.ReverseEmaOutputs(selectedBars, .3);
        using var context = new ComputeContext();
        foreach (var key in new[] { "EremaTrend", "EremaCycle" })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersReverseExponentialMovingAverageIndicatorV2, new EhlersReverseEmaIndicatorV2SpecOptions(.05, .3), key), context); Assert.NotNull(arm); Assert.Equal(key == "EremaTrend" ? expected : cycle, arm.Value.ToArray());
        }
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateEhlersReverseExponentialMovingAverageIndicatorV2(); Assert.Equal(expected, batch.OutputValues["EremaTrend"]); Assert.Equal(cycle, batch.OutputValues["EremaCycle"]);
        var single = Data(bars); single.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeEhlersReverseEmaIndicatorV1Fast(single, context, .05); Assert.Equal(expected, raw.ToArray());
    }
    [Fact]
    public void NaNAlphaIsRejectedBeforeComputation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersReverseExponentialMovingAverageIndicatorV1State(double.NaN));
        foreach (var trend in new[] { false, true })
        {
            var a = trend ? double.NaN : .05; var b = trend ? .3 : double.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersReverseExponentialMovingAverageIndicatorV2State(a, b));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { Candle(1) }).CalculateEhlersReverseExponentialMovingAverageIndicatorV2(a, b));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { Candle(1) }).CalculateEhlersReverseExponentialMovingAverageIndicatorV1(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.EhlersReverseEmaIndicatorV1(new[] { 1d }, new double[1], double.NaN));
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceEitherWave()
    {
        foreach (var variant in new[] { 1, 2 }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Create() => variant == 1 ? new EhlersReverseExponentialMovingAverageIndicatorV1State() : new EhlersReverseExponentialMovingAverageIndicatorV2State();
            var state = Create(); var control = Create(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 20).Select(i => Candle(i % 3)))
            { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
}
