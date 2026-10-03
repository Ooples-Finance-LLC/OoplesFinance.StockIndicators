using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveGravityNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveCenterOfGravityOscillator)).Select(c => new object[] { c });
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
    { var o = (EhlersAdaptiveCenterOfGravityOscillatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Eacog", BuiltInFormulaReferences.AdaptiveGravityValues(bars, o.Length) } }; }
    private static void Check(Bar[] bars, int length = 5)
    {
        var expected = BuiltInFormulaReferences.AdaptiveGravityValues(bars, length); var batch = Data(bars).CalculateEhlersAdaptiveCenterOfGravityOscillator(length); Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Eacog"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersAdaptiveCenterOfGravityOscillatorFast(Data(bars), context, length); Assert.Equal(expected, fast.ToArray());
        using var state = new EhlersAdaptiveCenterOfGravityOscillatorState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Eacog"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyPricesPreserveSignedMoments()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 8 }) foreach (var length in new[] { 1, 2, 5, 12 })
        { Check(Enumerable.Range(0, 65).Select(i => Candle((i % 5 + 1) * scale)).ToArray(), length); Check(Enumerable.Range(0, 65).Select(i => Candle(i % 2 == 0 ? scale : -scale)).ToArray(), length); }
    }
    [Fact]
    public void PowerOfTwoScalingPreservesAdaptiveGravity()
    {
        var prices = Enumerable.Range(0, 70).Select(i => (double)(i % 7 + 1)).ToArray(); var baseline = Data(prices.Select(v => Candle(v)).ToArray()).CalculateEhlersAdaptiveCenterOfGravityOscillator().CustomValuesList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) { var bars = prices.Select(v => Candle(v * scale)).ToArray(); Check(bars); Assert.Equal(baseline, Data(bars).CalculateEhlersAdaptiveCenterOfGravityOscillator().CustomValuesList); }
    }
    [Fact]
    public void ConstantPricesUseIntegerCenteringAndAvailableStartupHistory()
    {
        var bars = Enumerable.Repeat(Candle(1), 100).ToArray(); Check(bars); var periods = BuiltInFormulaReferences.AdaptiveCyberValues(bars, 5, .07)["Period"]; var line = Data(bars).CalculateEhlersAdaptiveCenterOfGravityOscillator().CustomValuesList;
        for (var i = 0; i < bars.Length; i++) { var window = (int)Math.Ceiling(periods[i] / 2); Assert.Equal((window + 1) / 2 - (Math.Min(window, i + 1) + 1) / 2d, line[i]); }
        Assert.Contains(-.5, line);
    }
    [Fact]
    public void ZeroSignedMassUsesTheZeroSentinel()
    {
        var bars = new[] { 1d, -1, 0, 2, -2, 0, 0 }.Select(v => Candle(v)).ToArray(); Check(bars); Assert.Equal(0, Data(bars).CalculateEhlersAdaptiveCenterOfGravityOscillator().CustomValuesList[1]);
        bars = Enumerable.Repeat(Candle(0), 75).ToArray(); Check(bars); Assert.All(Data(bars).CalculateEhlersAdaptiveCenterOfGravityOscillator().CustomValuesList, value => Assert.Equal(0, value));
    }
    [Fact]
    public void ExtremeMedianPeriodsAllocateOnlyConsumedHistory()
    { var bars = Enumerable.Range(0, 30).Select(i => Candle(i % 5 + 1)).ToArray(); foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, length); Check(Array.Empty<Bar>(), length); } }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastPaths()
    {
        var selected = Enumerable.Range(0, 45).Select(i => (double)(i % 7 + 1)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.AdaptiveGravityValues(selected.Select(v => Candle(v)).ToArray(), 3);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersAdaptiveCenterOfGravityOscillator(3); Assert.Equal(expected, data.CustomValuesList);
        using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected); using var fast = IndicatorCompute.ComputeEhlersAdaptiveCenterOfGravityOscillatorFast(data, context, 3); Assert.Equal(expected, fast.ToArray());
    }
    [Fact]
    public void InvalidFieldsLeavePeriodAndSignedMomentsUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersAdaptiveCenterOfGravityOscillatorState(); using var control = new EhlersAdaptiveCenterOfGravityOscillatorState(); state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 15).Select(i => Native(Candle(i % 3 + 1)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
