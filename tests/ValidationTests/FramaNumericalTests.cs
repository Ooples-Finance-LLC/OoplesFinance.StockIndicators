using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FramaNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersFrama) || c.IndicatorType == typeof(Frama)).Select(c => new object[] { c });
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
    { var o = ((IBuiltInIndicator)indicator).CreateOptions(); var length = o is FramaSpecOptions frama ? frama.Length : ((EhlersFramaSpecOptions)o).Length; return new() { { "Fama", BuiltInFormulaReferences.FramaValues(bars, length) } }; }
    private static void Check(Bar[] bars, int length = 20)
    {
        var expected = BuiltInFormulaReferences.FramaValues(bars, length); var batch = Data(bars).CalculateEhlersFractalAdaptiveMovingAverage(length);
        Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Fama"]); Assert.All(expected, v => Assert.True(double.IsFinite(v)));
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersFramaFast(Data(bars), context, length); Assert.Equal(expected, fast.ToArray());
        var core = new double[bars.Length]; MovingAverageCore.FractalAdaptiveMovingAverage(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length); Assert.Equal(expected, core);
        using var state = new EhlersFractalAdaptiveMovingAverageState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Fama"]); }
            }
        }
    }
    private static Bar Scale(Bar b, double scale) => new(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume);
    [Fact]
    public void WideAndTinyCandlesRetainDimensionAndFeedback()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var length in new[] { 2, 3, 20 })
            Check(Enumerable.Range(0, 55).Select(i => new Bar(DateTime.UnixEpoch, 0, scale, -scale, i % 3 == 0 ? -scale : scale, 1)).ToArray(), length);
        Check(Enumerable.Range(0, 60).Select(i => new Bar(DateTime.UnixEpoch, 0, i < 30 ? double.MaxValue : double.Epsilon, i < 30 ? -double.MaxValue : 0, i % 2 == 0 ? 0 : double.Epsilon, 1)).ToArray(), 4);
    }
    [Fact]
    public void PowerOfTwoScalingPreservesDimensionAndFilterTrajectory()
    {
        var bars = Enumerable.Range(0, 60).Select(i => new Bar(DateTime.UnixEpoch, i % 3, i % 7 == 0 ? 4 : 3, -2, i % 5 - 1, 1)).ToArray(); var baseline = Data(bars).CalculateEhlersFractalAdaptiveMovingAverage(4).CustomValuesList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var scaled = bars.Select(b => Scale(b, scale)).ToArray(); Assert.Equal(baseline.Select(v => v * scale), Data(scaled).CalculateEhlersFractalAdaptiveMovingAverage(4).CustomValuesList); Check(scaled, 4); }
    }
    [Fact]
    public void EqualHalvesPreserveKnownStartupAndFirstSmoothedValue()
    {
        var bars = new[] { 1d, 3, 2, 4, 6 }.Select(v => new Bar(DateTime.UnixEpoch, v, 10, 0, v, 1)).ToArray();
        foreach (var length in new[] { 3, 4 })
        { var line = Data(bars).CalculateEhlersFractalAdaptiveMovingAverage(length).CustomValuesList; Assert.Equal(new[] { 1d, 3, 2, 4 }, line.Take(4)); Assert.InRange(Math.Abs(line[4] - (4 + 2 * Math.Exp(-4.6))), 0, 1e-15); Check(bars, length); }
        foreach (var value in new[] { double.Epsilon, 1d, double.MaxValue })
        { var constant = Enumerable.Repeat(new Bar(DateTime.UnixEpoch, value, value, value, value, 1), 30).ToArray(); Assert.All(Data(constant).CalculateEhlersFractalAdaptiveMovingAverage(4).CustomValuesList, v => Assert.Equal(value, v)); Check(constant, 4); }
    }
    [Fact]
    public void ExpiringHalfRangesAndZeroRangesRetainThePreviousGain()
    {
        var bars = Enumerable.Range(0, 55).Select(i => new Bar(DateTime.UnixEpoch, 0, i < 25 ? i % 7 == 0 ? 10 : 4 : 1, i < 25 ? 0 : 1, i % 3, 1)).ToArray(); Check(bars, 4); Check(bars, 10);
        var line = Data(bars).CalculateEhlersFractalAdaptiveMovingAverage(4).CustomValuesList; Assert.NotEqual(bars[^1].Close, line[^1]);
    }
    [Fact]
    public void NormalizedAndLargestEvenPeriodsUseOnlyConsumedHistory()
    {
        var bars = Enumerable.Range(0, 15).Select(i => Candle(i % 3)).ToArray();
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 3, int.MaxValue - 1 }) { Check(bars, length); Check(Array.Empty<Bar>(), length); }
    }
    [Fact]
    public void UnrepresentableRoundedPeriodIsRejectedBeforeAllocation()
    {
        Assert.Throws<OverflowException>(() => new EhlersFractalAdaptiveMovingAverageState(int.MaxValue));
        Assert.Throws<OverflowException>(() => Data(Array.Empty<Bar>()).CalculateEhlersFractalAdaptiveMovingAverage(int.MaxValue));
        Assert.Throws<OverflowException>(() => MovingAverageCore.FractalAdaptiveMovingAverage(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), int.MaxValue));
        using var context = new ComputeContext(); Assert.Throws<OverflowException>(() => IndicatorCompute.ComputeEhlersFramaFast(Data(Array.Empty<Bar>()), context, int.MaxValue));
    }
    [Fact]
    public void SelectedPricesPreserveOriginalHighAndLowAcrossBothArms()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.FramaValues(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(), 4);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersFractalAdaptiveMovingAverage(4); Assert.Equal(expected, batch.CustomValuesList);
        foreach (var spec in new[] { new IndicatorSpec(IndicatorName.EhlersFractalAdaptiveMovingAverage, new EhlersFramaSpecOptions(4), "Fama"), new IndicatorSpec(IndicatorName.EhlersFractalAdaptiveMovingAverage, new FramaSpecOptions(4), "Fama") })
        { using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected); using var arm = IndicatorCompute.ComputeArm(data, spec, context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray()); }
        using var selectedState = new CustomInputState(new EhlersFractalAdaptiveMovingAverageState(4), bar => bar.Volume);
        for (var replay = 0; replay < 2; replay++)
        {
            selectedState.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var original = bars[i]; var bar = Native(new Bar(original.Time, original.Open, original.High, original.Low, original.Close, selected[i]));
                foreach (var commit in new[] { false, false, true }) Assert.Equal(expected[i], selectedState.Update(bar, commit, true).Value);
            }
        }
    }
    [Fact]
    public void InvalidFieldsLeaveRangesGainAndFilterUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersFractalAdaptiveMovingAverageState(4); using var control = new EhlersFractalAdaptiveMovingAverageState(4); var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
    [Fact]
    public void SubnormalFeedbackAccumulatesIntoARepresentableStep()
    {
        var bars = Enumerable.Range(0, 105).Select(i => new Bar(DateTime.UnixEpoch, 0, 4 * double.Epsilon, 0, i < 4 ? 0 : double.Epsilon, 1)).ToArray();
        Check(bars, 4);
        Assert.Equal(double.Epsilon, Data(bars).CalculateEhlersFractalAdaptiveMovingAverage(4).CustomValuesList[^1]);
    }
}
