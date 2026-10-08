using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PercentageTrendNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(PercentageTrend)).Select(c => new object[] { c });
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
    { var options = (PercentageTrendSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Pti", BuiltInFormulaReferences.PercentageTrendValues(bars, options.Length, options.Pct) } }; }
    private static void Check(Bar[] bars, int length, double pct)
    {
        var expected = BuiltInFormulaReferences.PercentageTrendValues(bars, length, pct);
        Assert.Equal(expected, Data(bars).CalculatePercentageTrend(length, pct).OutputValues["Pti"]);
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputePercentageTrendFast(Data(bars), context, length, pct);
        Assert.Equal(expected, result.ToArray());
        using var state = new PercentageTrendState(length, pct);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(-10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), commit, true).Outputs!["Pti"]);
            }
        }
    }
    [Fact]
    public void CrossingsAndWideThresholdsMatchRoundedRationalProducts()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var length in new[] { -1, 0, 1, 2, 7 }) foreach (var pct in new[] { -.3, 0, .15, 1, 2, double.MaxValue })
            Check(new[] { -scale, scale, 0, scale / 2, -scale / 2, scale }.Select(v => Candle(v)).ToArray(), length, pct);
        Check(Array.Empty<Bar>(), 3, .15);
        var hand = new[] { 1d, 3, 2 }.Select(v => Candle(v)).ToArray();
        Assert.Equal(new[] { 0d, 1.25, 2.25 }, BuiltInFormulaReferences.PercentageTrendValues(hand, 1, .25));
        Check(hand, 1, .25);
    }
    [Fact]
    public void NonfiniteParametersAreRejectedAcrossDirectRoutes()
    {
        foreach (var pct in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PercentageTrendState(pct: pct));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculatePercentageTrend(pct: pct));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputePercentageTrendFast(Data(Array.Empty<Bar>()), context, pct: pct));
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceWindowOrPreviousFilteredValue()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new PercentageTrendState(); using var control = new PercentageTrendState();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
