using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveLaguerreNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveLaguerreFilter)).Select(c => new object[] { c });
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
    { var o = (EhlersAdaptiveLaguerreFilterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Ealf", BuiltInFormulaReferences.AdaptiveLaguerreValues(bars, o.Length) } }; }
    private static void Check(Bar[] bars, int length = 14, int median = 5)
    {
        var expected = BuiltInFormulaReferences.AdaptiveLaguerreValues(bars, length, median); var batch = Data(bars).CalculateEhlersAdaptiveLaguerreFilter(length, median);
        Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Ealf"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersAdaptiveLaguerreFilterFast(Data(bars), context, length, median); Assert.Equal(expected, fast.ToArray());
        using var state = new EhlersAdaptiveLaguerreFilterState(length, median);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Ealf"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyPricesRetainRankMedianAndStageHistory()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var lengths in new[] { (1, 1), (3, 2), (14, 5) })
            Check(Enumerable.Range(0, 40).Select(i => Candle(i < 25 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), lengths.Item1, lengths.Item2);
    }
    [Fact]
    public void ConstantInputsAndFirstBarAreExactlyPreserved()
    {
        foreach (var value in new[] { 0d, double.Epsilon, -double.Epsilon, 1d, double.MaxValue, -double.MaxValue })
        { var bars = Enumerable.Repeat(Candle(value), 25).ToArray(); Assert.All(Data(bars).CalculateEhlersAdaptiveLaguerreFilter().CustomValuesList, v => Assert.Equal(value, v)); Check(bars); }
    }
    [Fact]
    public void PowerOfTwoScalingPreservesTheAdaptiveTrajectory()
    {
        var bars = Enumerable.Range(0, 60).Select(i => Candle(i < 35 ? i % 5 - 2 : 0)).ToArray(); var baseline = Data(bars).CalculateEhlersAdaptiveLaguerreFilter().CustomValuesList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var scaled = bars.Select(b => Candle(b.Close * scale)).ToArray(); Assert.Equal(baseline.Select(v => v * scale), Data(scaled).CalculateEhlersAdaptiveLaguerreFilter().CustomValuesList); Check(scaled); }
    }
    [Fact]
    public void BothPeriodsNormalizeAndAllocateOnlyConsumedHistory()
    {
        var bars = Enumerable.Range(0, 20).Select(i => Candle(i % 3)).ToArray();
        foreach (var pair in new[] { (int.MinValue, 0), (0, int.MaxValue), (int.MaxValue, -1), (int.MaxValue, int.MaxValue), (14, 1), (1, 5) })
        { Check(bars, pair.Item1, pair.Item2); Check(Array.Empty<Bar>(), pair.Item1, pair.Item2); }
    }
    [Fact]
    public void RankExtremaMedianExpirationAndZeroRanksRetainTheirSemantics()
    {
        var bars = Enumerable.Range(0, 90).Select(i => Candle(i < 15 ? 0 : i < 40 ? i % 7 : i < 60 ? 3 : i % 11)).ToArray();
        foreach (var pair in new[] { (3, 2), (3, 4), (14, 5), (20, 1) }) Check(bars, pair.Item1, pair.Item2);
    }
    [Fact]
    public void PriceScaleTieBandPreventsNoiseFromChangingGain()
    {
        foreach (var offset in new[] { 1d, 1000000d })
            Check(Enumerable.Range(0, 45).Select(i => Candle(offset + (i % 9 - 4) * offset * Math.Pow(2, -52))).ToArray(), 14, 5);
    }
    [Fact]
    public void SelectedPricesReachBatchAndTypedArm()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.AdaptiveLaguerreValues(selected.Select(v => Candle(v)).ToArray(), 3); var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersAdaptiveLaguerreFilter(3); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected); using var arm = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersAdaptiveLaguerreFilter, new EhlersAdaptiveLaguerreFilterSpecOptions(3), "Ealf"), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
    }
    [Fact]
    public void InvalidFieldsLeaveRanksMedianAndStagesUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersAdaptiveLaguerreFilterState(3, 2); using var control = new EhlersAdaptiveLaguerreFilterState(3, 2); var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
    [Fact]
    public void SubnormalStepAccumulatesWithoutLosingTheFourStageTail()
    {
        var bars = new[] { Candle(0) }.Concat(Enumerable.Repeat(Candle(double.Epsilon), 45)).ToArray();
        Check(bars, 14, 5);
        Assert.Equal(double.Epsilon, Data(bars).CalculateEhlersAdaptiveLaguerreFilter(14, 5).CustomValuesList[^1]);
    }
}
