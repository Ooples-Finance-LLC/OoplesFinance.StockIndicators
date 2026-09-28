using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ModifiedRsiNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersModifiedRelativeStrengthIndex)).Select(c => new object[] { c });
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
    { var o = (EhlersModifiedRelativeStrengthIndexSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.ModifiedRsiValues(bars, o.Length1, o.Length2, o.Length3); }
    private static Dictionary<string, double[]> Check(Bar[] bars, int first = 48, int second = 10, int third = 10)
    {
        var expected = BuiltInFormulaReferences.ModifiedRsiValues(bars, first, second, third); var batch = Data(bars).CalculateEhlersModifiedRelativeStrengthIndex(first, second, third); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]); Assert.Equal(expected["Emrsi"], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Keys) { using var fast = IndicatorCompute.ComputeModifiedRsiFast(Data(bars), context, new(first, second, third), key); Assert.Equal(expected[key], fast.ToArray()); }
        using var state = new EhlersModifiedRelativeStrengthIndexState(first, second, third);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Emrsi"][i], point.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideAndTinyRoofingChangesRetainBothOutputs()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue }) foreach (var period in new[] { 1, 2, 10 })
        {
            var result = Check(Enumerable.Range(0, 80).Select(i => Candle(i % 3 == 0 ? -scale : scale)).ToArray(), 48, period, 7);
            Assert.Contains(result["Emrsi"], v => v != 0); Assert.Contains(result["Signal"], v => v != 0);
        }
        Check(new[] { double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon, double.Epsilon }.Select(v => Candle(v)).ToArray());
    }
    [Fact]
    public void PowerOfTwoScalingPreservesTheRatioAndSignal()
    {
        var prices = Enumerable.Range(0, 80).Select(i => (double)(i % 7 - 3)).ToArray(); var baseline = Check(prices.Select(v => Candle(v)).ToArray());
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var actual = Check(prices.Select(v => Candle(v * scale)).ToArray()); foreach (var key in baseline.Keys) Assert.Equal(baseline[key], actual[key]); }
    }
    [Fact]
    public void StartupRequiresTwoDefinedRatiosAndZeroHistoryStaysZero()
    {
        var zeros = Check(Enumerable.Repeat(Candle(0), 80).ToArray()); foreach (var values in zeros.Values) Assert.All(values, v => Assert.Equal(0, v));
        var result = Check(Enumerable.Range(0, 80).Select(i => Candle(i < 3 ? 0 : i % 5 + 1)).ToArray(), 1, 1, 1);
        Assert.All(result["Emrsi"].Take(4), v => Assert.Equal(0, v)); Assert.Contains(result["Emrsi"].Skip(4), v => v != 0);
    }
    [Fact]
    public void ExtremePeriodsRetainGainWithoutEagerHistoryAllocation()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var period in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, period, period, period); Check(Array.Empty<Bar>(), period, period, period); }
        var result = Check(Enumerable.Range(0, 35).Select(i => Candle(i + 1)).ToArray(), int.MaxValue, int.MaxValue, int.MaxValue); Assert.Contains(result["Emrsi"], v => v > 0); Assert.Contains(result["Signal"], v => v > 0);
    }
    [Fact]
    public void BelowRangeRoofingTailsStillDetermineTheGainLossRatio()
    {
        foreach (var scale in new[] { 1d, double.Epsilon })
        {
            var result = Check(Enumerable.Range(0, 300).Select(i => Candle((i % 2 == 0 ? 2 : 3) * scale)).ToArray(), 1, 1, 1);
            Assert.All(result["Emrsi"].Skip(250), v => Assert.InRange(v, 1 - 1e-12, 1 + 1e-12));
        }
    }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastPaths()
    {
        var selected = Enumerable.Range(0, 65).Select(i => (double)(i % 7 - 3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.ModifiedRsiValues(selected.Select(v => Candle(v)).ToArray(), 3, 8, 5);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersModifiedRelativeStrengthIndex(3, 8, 5); foreach (var key in expected.Keys) Assert.Equal(expected[key], data.OutputValues[key]);
        using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected); foreach (var key in expected.Keys) { using var fast = IndicatorCompute.ComputeModifiedRsiFast(data, context, new(3, 8, 5), key); Assert.Equal(expected[key], fast.ToArray()); }
    }
    [Fact]
    public void InvalidFieldsLeaveRoofingRatiosAndSignalUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersModifiedRelativeStrengthIndexState(); using var control = new EhlersModifiedRelativeStrengthIndexState(); state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 25).Select(i => Native(Candle(i % 3 - 1)))) { var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
