using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SelfAdjustingLaguerreNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha)).Select(c => new object[] { c });
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
    { var o = (EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Elrsiwsa", BuiltInFormulaReferences.SelfAdjustingLaguerreValues(bars, o.Length) } }; }
    private static void Check(Bar[] bars, int length = 13)
    {
        var expected = BuiltInFormulaReferences.SelfAdjustingLaguerreValues(bars, length); var batch = Data(bars).CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(length);
        Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Elrsiwsa"]); Assert.All(expected, v => Assert.InRange(v, 0d, 1d));
        var spec = new IndicatorSpec(IndicatorName.EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha, new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaSpecOptions(length), "Elrsiwsa");
        using var context = new ComputeContext(); using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        using var state = new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Elrsiwsa"]); }
            }
        }
    }
    private static Bar Scale(Bar b, double scale) => new(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume);
    [Fact]
    public void WideAndTinyCandlesRetainSourceGainAndStages()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var length in new[] { 1, 3, 13 })
        {
            var bars = Enumerable.Range(0, 35).Select(i => new Bar(DateTime.UnixEpoch, i % 2 == 0 ? -scale : scale, scale, -scale, i % 3 == 0 ? -scale : scale, 1)).ToArray(); Check(bars, length);
        }
    }
    [Fact]
    public void PowerOfTwoScalingPreservesGainAndNormalizedTrajectory()
    {
        var bars = Enumerable.Range(0, 45).Select(i => new Bar(DateTime.UnixEpoch, i % 3 - 1, 4, -3, i % 5 - 2, 1)).ToArray(); var expected = Data(bars).CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha().CustomValuesList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var scaled = bars.Select(b => Scale(b, scale)).ToArray(); Assert.Equal(expected, Data(scaled).CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha().CustomValuesList); Check(scaled); }
    }
    [Fact]
    public void GapRatiosRetainValuesBeyondBinary64Range()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, 1) }
            .Concat(Enumerable.Range(0, 20).Select(i => new Bar(DateTime.UnixEpoch, 0, double.Epsilon, 0, i % 2 * double.Epsilon, 1))).ToArray();
        Check(bars, 1); Check(bars, 3);
    }
    [Fact]
    public void ZeroRangesAndConstantTailKeepTheDeadbandContract()
    {
        var zero = Enumerable.Repeat(new Bar(DateTime.UnixEpoch, 0, 0, 0, 0, 1), 10).ToArray(); Assert.All(Data(zero).CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha().CustomValuesList, v => Assert.Equal(0d, v)); Check(zero);
        var bars = Enumerable.Range(0, 500).Select(i => new Bar(DateTime.UnixEpoch, 1, 2, 0, 1, 1)).ToArray(); Check(bars, 1);
        Assert.All(Data(bars).CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(1).CustomValuesList.Skip(450), v => Assert.Equal(0d, v));
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyConsumedHistory()
    {
        var bars = Enumerable.Range(0, 15).Select(i => new Bar(DateTime.UnixEpoch, i % 3, 4, -2, i % 5 - 1, 1)).ToArray();
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(bars, length); Check(Array.Empty<Bar>(), length); }
    }
    [Fact]
    public void ExpiringRangesAndRatiosChangeTheAdaptiveGain()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch, i % 4, i % 7 == 0 ? 100 : 4, i % 5 == 0 ? -100 : -2, i % 3, 1)).ToArray(); Check(bars, 3); Check(bars, 7);
    }
    [Fact]
    public void SelectedPricesPreserveOriginalOpenHighAndLow()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.SelfAdjustingLaguerreValues(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(), 3);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha(3); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected); using var arm = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha, new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaSpecOptions(3), "Elrsiwsa"), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
    }
    [Fact]
    public void InvalidFieldsLeaveRangesRatiosAndStagesUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState(3); using var control = new EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlphaState(3); var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
