using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TruncatedBandPassNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersTruncatedBandPassFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWindow(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var options = (EhlersTruncatedBandPassFilterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Etbpf", BuiltInFormulaReferences.TruncatedBandPassValues(bars, options.Length1, options.Length2, options.Bandwidth) } }; }
    private static void Check(Bar[] bars, int length1, int length2, double bw)
    {
        var expected = BuiltInFormulaReferences.TruncatedBandPassValues(bars, length1, length2, bw);
        Assert.Equal(expected, Data(bars).CalculateEhlersTruncatedBandPassFilter(length1, length2, bw).OutputValues["Etbpf"]);
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersTruncatedBandPassFilterFast(Data(bars), context, length1, length2, bw);
        Assert.Equal(expected, result.ToArray());
        var core = new double[bars.Length]; OscillatorCore.EhlersTruncatedBandPassFilter(bars.Select(b => b.Close).ToArray(), core, length1, length2, bw); Assert.Equal(expected, core);
        using var state = new EhlersTruncatedBandPassFilterState(length1, length2, bw);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(-10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), commit, true).Outputs!["Etbpf"]);
            }
        }
    }
    [Fact]
    public void TruncationRetainsWideDifferencesAndGrowsHistoryWithObservedBars()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var periods in new[] { (1, 1), (2, 2), (20, 3), (20, 10), (int.MaxValue, int.MaxValue) })
        foreach (var bandwidth in new[] { -.1, 0, .1, 10d })
        {
            var bars = Enumerable.Range(0, 24).Select(i => Candle(i < 6 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray();
            Check(bars, periods.Item1, periods.Item2, bandwidth); Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, bandwidth);
        }
    }
    [Fact]
    public void LargeFiniteBandwidthUsesARepresentableDividedPhase()
    { Check(Enumerable.Range(0, 24).Select(i => Candle(i % 3)).ToArray(), 20, 3, double.MaxValue); }
    [Fact]
    public void TwoTapImpulseHasExactlyFourNonzeroSlots()
    {
        var cosine = Math.Cos(.1 * 2 * Math.PI / 20);
        var decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        var drive = .5 * (1 - decay); var feedback = Math.Cos(2 * Math.PI / 20) * (1 + decay);
        var bars = Enumerable.Range(0, 8).Select(i => Candle(i == 0 ? 1 : 0)).ToArray();
        Assert.Equal(new[] { drive, feedback * drive, -drive, -(feedback * drive), 0, 0, 0, 0 }, BuiltInFormulaReferences.TruncatedBandPassValues(bars, 20, 2, .1));
        Check(bars, 20, 2, .1);
    }
    [Fact]
    public void SelectedValuesDriveBatchAndFastTruncation()
    {
        var selected = Enumerable.Range(0, 30).Select(i => (double)(i % 7)).ToList();
        var original = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.TruncatedBandPassValues(selected.Select(v => Candle(v)).ToArray(), 20, 3, .1);
        var batch = Data(original); batch.SetCustomValues(selected); batch.CalculateEhlersTruncatedBandPassFilter(length2: 3);
        Assert.Equal(expected, batch.OutputValues["Etbpf"]);
        var data = Data(original); data.SetCustomValues(selected); using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeEhlersTruncatedBandPassFilterFast(data, context, length2: 3); Assert.Equal(expected, result.ToArray());
    }
    [Fact]
    public void UnrepresentableCoefficientArgumentsAreRejectedBeforeCalculation()
    {
        foreach (var bw in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersTruncatedBandPassFilterState(length1: 1, bw: bw));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersTruncatedBandPassFilter(length1: 1, bw: bw));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeEhlersTruncatedBandPassFilterFast(Data(Array.Empty<Bar>()), context, length1: 1, bw: bw));
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceWindowOrPreviousFilteredValue()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersTruncatedBandPassFilterState(); using var control = new EhlersTruncatedBandPassFilterState();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
