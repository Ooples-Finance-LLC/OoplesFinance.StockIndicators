using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EmpiricalDecompositionNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersEmpiricalModeDecomposition)).Select(c => new object[] { c });
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
    { var o = (EhlersEmpiricalModeDecompositionSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.EmpiricalDecompositionValues(bars, o.Length1, o.Length2, o.Delta, o.Fraction, o.MaType); }
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage };
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 8, int extrema = 5, double delta = .5, double fraction = .1, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.EmpiricalDecompositionValues(bars, length, extrema, delta, fraction, kind); var batch = Data(bars).CalculateEhlersEmpiricalModeDecomposition(kind, length, extrema, delta, fraction); Assert.Empty(batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Keys) { using var result = IndicatorCompute.ComputeEhlersEmpiricalModeDecompositionFast(Data(bars), context, length, extrema, delta, fraction, kind, key); Assert.Equal(expected[key], result.ToArray()); Assert.Equal(expected[key], batch.OutputValues[key]); }
        using var state = new EhlersEmpiricalModeDecompositionState(kind, length, extrema, delta, fraction);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in new[] { 1d, -5, 4, -2, 8, 1 }) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Trend"][i], point.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideAndSubnormalInputsPreserveAllThreeOutputs()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, 32 * double.Epsilon, 1d, Math.Pow(2, 1000), double.MaxValue })
            Check(Enumerable.Range(0, 65).Select(i => Candle(i % 5 < 2 ? -scale : scale)).ToArray(), kind: kind);
    }
    [Fact]
    public void FractionIsAppliedBeforePublishingTinyAndWideExtrema()
    {
        foreach (var kind in Kinds) foreach (var pair in new[] { (double.Epsilon, 1e300), (double.MaxValue, 1e-300), (1e-300, 1e300), (1e300, 1e-300), (1d, -2d) })
        {
            var result = Check(Enumerable.Range(0, 55).Select(i => Candle(i % 5 < 2 ? -pair.Item1 : pair.Item1)).ToArray(), fraction: pair.Item2, kind: kind);
            Assert.Contains(result["Peak"], v => double.IsFinite(v) && v != 0); Assert.Contains(result["Valley"], v => double.IsFinite(v) && v != 0);
        }
    }
    [Fact]
    public void ZeroFractionDoesNotTurnLargeInternalExtremaIntoNan()
    {
        foreach (var kind in Kinds)
        {
            var result = Check(Enumerable.Range(0, 70).Select(i => Candle(i % 3 == 0 ? -double.MaxValue : double.MaxValue)).ToArray(), fraction: 0, kind: kind);
            Assert.All(result["Peak"], v => Assert.Equal(0, v)); Assert.All(result["Valley"], v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void ExtremePeriodsUseOnlyConsumedHistory()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue })
        { Check(bars, length, length, kind: kind); Check(Array.Empty<Bar>(), length, length, kind: kind); }
    }
    [Fact]
    public void ConstantBandsKeepPeaksAndValleysAtZero()
    {
        foreach (var kind in Kinds) foreach (var close in new[] { 0d, 3, double.MaxValue }) foreach (var values in Check(Enumerable.Repeat(Candle(close), 45).ToArray(), kind: kind).Values) Assert.All(values, v => Assert.Equal(0, v));
        var result = Check(new[] { 1d, 3, 7, -5, 4, -2, 1, 9, -4, 0, 3, -8 }.Select(v => Candle(v)).ToArray(), 3, 2);
        Assert.Contains(result["Peak"], v => v > 0); Assert.Contains(result["Valley"], v => v < 0);
    }
    [Fact]
    public void SelectedPricesReachEveryFastAndBatchOutput()
    {
        var selected = Enumerable.Range(0, 45).Select(i => (double)(i % 5 - 2)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var kind in Kinds)
        {
            var expected = BuiltInFormulaReferences.EmpiricalDecompositionValues(selected.Select(v => Candle(v)).ToArray(), 3, 2, .5, .1, kind); var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersEmpiricalModeDecomposition(kind, 3, 2);
            foreach (var key in expected.Keys) { Assert.Equal(expected[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersEmpiricalModeDecompositionFast(source, context, 3, 2, .5, .1, kind, key); Assert.Equal(expected[key], result.ToArray()); }
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceExtremaOrAverages()
    {
        foreach (var kind in Kinds) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersEmpiricalModeDecompositionState(kind, 3, 2); using var control = new EhlersEmpiricalModeDecompositionState(kind, 3, 2);
            for (var i = 0; i < 10; i++) { state.Update(Native(Candle(i % 4)), true, false); control.Update(Native(Candle(i % 4)), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 15; i++) { var bar = Native(Candle(i % 5 - 2)); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in new[] { "Trend", "Peak", "Valley" }) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void CallbacksReceiveBandThenHeldPeaksAndValleys()
    {
        var bars = new[] { 1d, 3, 5, -2, 4, 1, -7, 3 }.Select(v => Candle(v)).ToArray(); var band = BuiltInFormulaReferences.TrendExtractionValues(bars, 3, .5, Kinds[0])["Bp"]; var peaks = new double[bars.Length]; var valleys = new double[bars.Length];
        for (var i = 1; i < bars.Length; i++) { var older = i < 2 ? 0 : band[i - 2]; peaks[i] = band[i - 1] > band[i] && band[i - 1] > older ? band[i - 1] : peaks[i - 1]; valleys[i] = band[i - 1] < band[i] && band[i - 1] < older ? band[i - 1] : valleys[i - 1]; }
        foreach (var fast in new[] { false, true }) foreach (var key in new[] { "Trend", "Peak", "Valley" })
        {
            var stage = 0; var expectedInputs = new[] { band, peaks, valleys }; var callbacks = Enumerable.Range(0, 3).Select(slot => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((input, period) => { Assert.Equal(slot, stage++); Assert.Equal(slot == 0 ? 6 : 2, period); Assert.Equal(expectedInputs[slot], input); return Enumerable.Repeat(10d * (slot + 1), bars.Length).ToArray(); })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks); double[] actual;
            if (fast) { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersEmpiricalModeDecompositionFast(Data(bars), context, 3, 2, .5, .1, Kinds[0], key); actual = result.ToArray(); }
            else actual = Data(bars).CalculateEhlersEmpiricalModeDecomposition(Kinds[0], 3, 2).OutputValues[key].ToArray();
            Assert.Equal(3, stage); Assert.All(actual, v => Assert.Equal(key == "Trend" ? 10 : key == "Peak" ? 2 : 3, v));
        }
    }
    [Fact]
    public void NonfiniteFractionAndWidthAreRejected()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) { Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersEmpiricalModeDecompositionState(fraction: invalid)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersEmpiricalModeDecompositionState(delta: invalid)); }
    }
}
