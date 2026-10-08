using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CycleTunedRsiNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DominantCycleTunedRelativeStrengthIndex)).Select(c => new object[] { c });
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
    { var o = (DominantCycleTunedRelativeStrengthIndexSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "DctRsi", BuiltInFormulaReferences.CycleTunedRsiValues(bars, o.Length) } }; }
    private static void Check(Bar[] bars, int length = 5)
    {
        var expected = BuiltInFormulaReferences.CycleTunedRsiValues(bars, length); Assert.All(expected, v => Assert.InRange(v, 0, 100)); var batch = Data(bars).CalculateDominantCycleTunedRelativeStrengthIndex(length); Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["DctRsi"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeDominantCycleTunedRsiFast(Data(bars), context, length); Assert.Equal(expected, fast.ToArray());
        using var state = new DominantCycleTunedRelativeStrengthIndexState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["DctRsi"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyChangesPreserveAdaptiveGainLossRatios()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue }) foreach (var length in new[] { 1, 2, 5, 12 })
            Check(Enumerable.Range(0, 60).Select(i => Candle(i % 3 == 0 ? -scale : scale)).ToArray(), length);
        Check(new[] { double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon, double.Epsilon }.Select(v => Candle(v)).ToArray());
    }
    [Fact]
    public void FirstChangeSeedsTheAverageAndZeroLossUsesOneHundred()
    {
        foreach (var value in new[] { -1d, 0, 1d }) { var bars = Enumerable.Repeat(Candle(value), 30).ToArray(); Check(bars); Assert.All(Data(bars).CalculateDominantCycleTunedRelativeStrengthIndex().CustomValuesList, v => Assert.Equal(value < 0 ? 0 : 100, v)); }
        Check(new[] { 4d, 3, -2, 1, 0, -1 }.Select(v => Candle(v)).ToArray());
    }
    [Fact]
    public void TinyPositiveGainsAreNotErasedByRsiSubtraction()
    {
        var bars = new[] { -1d, Math.BitIncrement(-1d), -1d, Math.BitIncrement(-1d) }.Select(v => Candle(v)).ToArray(); Check(bars);
        var line = Data(bars).CalculateDominantCycleTunedRelativeStrengthIndex().CustomValuesList; Assert.True(line[1] > 0 && line[1] < 1e-14);
    }
    [Fact]
    public void SubnormalAveragesStillFormANonzeroRatio()
    {
        var bars = new[] { 0d, double.Epsilon, -double.Epsilon, double.Epsilon, 0 }.Select(v => Candle(v)).ToArray(); Check(bars);
        var line = Data(bars).CalculateDominantCycleTunedRelativeStrengthIndex().CustomValuesList; Assert.InRange(line[2], 1e-10, 100 - 1e-10);
    }
    [Fact]
    public void PowerOfTwoScalingPreservesTheBoundedRsiTrajectory()
    {
        var prices = Enumerable.Range(0, 60).Select(i => (double)(i % 7 - 3)).ToArray(); var baseline = Data(prices.Select(v => Candle(v)).ToArray()).CalculateDominantCycleTunedRelativeStrengthIndex().CustomValuesList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) { var bars = prices.Select(v => Candle(v * scale)).ToArray(); Check(bars); Assert.Equal(baseline, Data(bars).CalculateDominantCycleTunedRelativeStrengthIndex().CustomValuesList); }
    }
    [Fact]
    public void ExtremeMedianPeriodsAllocateOnlyConsumedHistory()
    { var bars = Enumerable.Range(0, 20).Select(i => Candle(i % 5 - 2)).ToArray(); foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, length); Check(Array.Empty<Bar>(), length); } }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastPaths()
    {
        var selected = Enumerable.Range(0, 40).Select(i => (double)(i % 7 - 3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.CycleTunedRsiValues(selected.Select(v => Candle(v)).ToArray(), 3);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateDominantCycleTunedRelativeStrengthIndex(3); Assert.Equal(expected, data.CustomValuesList);
        using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected); using var fast = IndicatorCompute.ComputeDominantCycleTunedRsiFast(data, context, 3); Assert.Equal(expected, fast.ToArray());
    }
    [Fact]
    public void InvalidFieldsLeavePeriodAndAveragesUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new DominantCycleTunedRelativeStrengthIndexState(); using var control = new DominantCycleTunedRelativeStrengthIndexState(); state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 15).Select(i => Native(Candle(i % 3 - 1)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
