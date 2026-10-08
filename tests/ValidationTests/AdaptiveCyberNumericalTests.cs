using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveCyberNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveCyberCycle)).Select(c => new object[] { c });
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
    private static IReadOnlyDictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = (EhlersAdaptiveCyberCycleSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.AdaptiveCyberValues(bars, o.Length, o.Alpha); }
    private static void Check(Bar[] bars, int length = 5, double alpha = .07)
    {
        var expected = BuiltInFormulaReferences.AdaptiveCyberValues(bars, length, alpha); var batch = Data(bars).CalculateEhlersAdaptiveCyberCycle(length, alpha);
        using var context = new ComputeContext();
        foreach (var key in new[] { "Eacc", "Period" })
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var fast = IndicatorCompute.ComputeEhlersAdaptiveCyberCycleFast(Data(bars), context, length, alpha, key == "Period"); Assert.Equal(expected[key], fast.ToArray());
        }
        Assert.Equal(expected["Eacc"], batch.CustomValuesList);
        using var state = new EhlersAdaptiveCyberCycleState(length, alpha);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Eacc"][i], point.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyPricesPreserveQuadratureAndAdaptiveCycle()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 32 }) foreach (var length in new[] { 1, 2, 5, 12 })
            Check(Enumerable.Range(0, 60).Select(i => Candle((i % 5 - 2) * scale)).ToArray(), length);
        Check(Enumerable.Range(0, 60).Select(i => Candle(i < 30 ? (i % 3 - 1) * double.MaxValue / 32 : (i % 3 - 1) * double.Epsilon)).ToArray());
    }
    [Fact]
    public void PowerOfTwoScalingPreservesThePeriodTrajectory()
    {
        var prices = Enumerable.Range(0, 70).Select(i => (double)(i % 7 - 3)).ToArray(); var baseline = Data(prices.Select(v => Candle(v)).ToArray()).CalculateEhlersAdaptiveCyberCycle().OutputValues["Period"];
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var bars = prices.Select(v => Candle(v * scale)).ToArray(); Check(bars); Assert.Equal(baseline, Data(bars).CalculateEhlersAdaptiveCyberCycle().OutputValues["Period"]); }
    }
    [Fact]
    public void StartupUsesTheSecondDifferenceAndZeroPhaseHasABoundedPeriod()
    {
        var ramp = Enumerable.Range(1, 7).Select(i => Candle(4 * i)).ToArray(); Check(ramp); Assert.Equal(new[] { 1d, 0, 0, 0, 0, 0, 0 }, Data(ramp).CalculateEhlersAdaptiveCyberCycle().CustomValuesList);
        var bars = Enumerable.Repeat(Candle(0), 100).ToArray(); Check(bars); var output = Data(bars).CalculateEhlersAdaptiveCyberCycle(); Assert.All(output.CustomValuesList, v => Assert.Equal(0, v)); Assert.Equal(.15 * (.33 * (6.28318 / .1 + .5)), output.OutputValues["Period"][0]); Assert.All(output.OutputValues["Period"], v => Assert.InRange(v, 0, 64));
    }
    [Fact]
    public void MedianPeriodsNormalizeAndGrowOnlyWithConsumedHistory()
    { var bars = Enumerable.Range(0, 20).Select(i => Candle(i % 7 - 3)).ToArray(); foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, length); Check(Array.Empty<Bar>(), length); } }
    [Fact]
    public void FiniteAlphaChangesAreHonoredAndNonfiniteAlphaIsRejected()
    {
        var bars = Enumerable.Range(0, 40).Select(i => Candle(i % 5 - 2)).ToArray(); foreach (var alpha in new[] { 0d, .07, .2, 1d }) Check(bars, 5, alpha);
        foreach (var alpha in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveCyberCycleState(5, alpha));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersAdaptiveCyberCycle(5, alpha));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeEhlersAdaptiveCyberCycleFast(Data(Array.Empty<Bar>()), context, 5, alpha));
        }
    }
    [Fact]
    public void SelectedPricesReachBothPublishedFastOutputs()
    {
        var selected = Enumerable.Range(0, 40).Select(i => (double)(i % 7 - 3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.AdaptiveCyberValues(selected.Select(v => Candle(v)).ToArray(), 3, .2);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersAdaptiveCyberCycle(3, .2); foreach (var key in expected.Keys) Assert.Equal(expected[key], data.OutputValues[key]);
        using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected);
        foreach (var key in expected.Keys) { using var arm = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersAdaptiveCyberCycle, new EhlersAdaptiveCyberCycleSpecOptions(3, .2), key), context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray()); }
    }
    [Fact]
    public void InvalidFieldsLeavePhasesMedianAndFeedbackUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersAdaptiveCyberCycleState(); using var control = new EhlersAdaptiveCyberCycleState(); state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 15).Select(i => Native(Candle(i % 3)))) { var actual = state.Update(bar, true, true); var expected = control.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Period"], actual.Outputs!["Period"]); }
        }
    }
}
