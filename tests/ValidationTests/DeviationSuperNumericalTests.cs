using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DeviationSuperNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersDeviationScaledSuperSmoother)).Select(c => new object[] { c });
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
    { var o = (EhlersDeviationScaledSuperSmootherSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Edsss", BuiltInFormulaReferences.DeviationSuperValues(bars, o.Length, 50, o.MaType) } }; }
    private static void Check(Bar[] bars, int length = 5, int rms = 7, MovingAvgType kind = MovingAvgType.EhlersHannMovingAverage)
    {
        var expected = BuiltInFormulaReferences.DeviationSuperValues(bars, length, rms, kind);
        var batch = Data(bars).CalculateEhlersDeviationScaledSuperSmoother(kind, length, rms); Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Edsss"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersDeviationScaledSuperSmootherFast(Data(bars), context, length, kind, rms); Assert.Equal(expected, fast.ToArray());
        using var state = new EhlersDeviationScaledSuperSmootherState(kind, length, rms);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(1)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs!["Edsss"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyMomentumRetainsRmsAndAdaptiveFeedback()
    {
        foreach (var kind in new[] { MovingAvgType.EhlersHannMovingAverage, MovingAvgType.WeightedMovingAverage }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 4 }) foreach (var length in new[] { 1, 5, 12 })
            Check(Enumerable.Range(0, 60).Select(i => Candle(i % 3 == 0 ? -scale : scale)).ToArray(), length, 7, kind);
        Check(new[] { double.MaxValue / 4, double.MaxValue / 4, 0, double.Epsilon, 2 * double.Epsilon, 3 * double.Epsilon }.Select(v => Candle(v)).ToArray(), 1, 7);
        Check(Enumerable.Range(0, 80).Select(i => Candle(i < 35 ? i % 2 == 0 ? double.MaxValue / 4 : -double.MaxValue / 4 : double.Epsilon)).ToArray());
    }
    [Fact]
    public void ZeroMomentumReturnsToTheNominalStableCutoff()
    {
        var constant = Enumerable.Repeat(Candle(1), 180).ToArray(); Check(constant, 5, 7); var line = Data(constant).CalculateEhlersDeviationScaledSuperSmoother(length1: 5, length2: 7).CustomValuesList;
        Assert.InRange(Math.Abs(line[^1] - 1), 0, 1e-14); Check(Enumerable.Repeat(Candle(0), 30).ToArray());
        var angle = Math.Sqrt(2) * Math.PI / 5; var radius = Math.Exp(-angle); var gain = 1 - 2 * radius * Math.Cos(angle) + radius * radius;
        Assert.InRange(Math.Abs(line[0] - gain / 2), 0, 1e-16);
    }
    [Fact]
    public void SubnormalStepAccumulatesThroughBothFeedbackStages()
    {
        var bars = Enumerable.Range(0, 140).Select(i => Candle(i < 4 ? 0 : double.Epsilon)).ToArray();
        Check(bars, 50, 1);
        Assert.Equal(double.Epsilon, Data(bars).CalculateEhlersDeviationScaledSuperSmoother(length1: 50, length2: 1).CustomValuesList[^1]);
    }
    [Fact]
    public void IndependentPeriodsNormalizeBeforeLagAndRmsSelection()
    { foreach (var length in new[] { int.MinValue, 0, 1, 6 }) foreach (var rms in new[] { int.MinValue, 0, 1, 8, int.MaxValue }) { Check(Enumerable.Range(0, 20).Select(i => Candle(i % 4)).ToArray(), length, rms); Check(Array.Empty<Bar>(), length, rms); } }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastPaths()
    {
        var selected = Enumerable.Range(0, 50).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.DeviationSuperValues(selected.Select(v => Candle(v)).ToArray(), 5, 7, MovingAvgType.EhlersHannMovingAverage);
        var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersDeviationScaledSuperSmoother(length1: 5, length2: 7); Assert.Equal(expected, data.CustomValuesList);
        using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected); using var fast = IndicatorCompute.ComputeEhlersDeviationScaledSuperSmootherFast(data, context, 5, length2: 7); Assert.Equal(expected, fast.ToArray());
    }
    [Fact]
    public void ComponentOverridesStillReceiveTheMomentumSeries()
    {
        var bars = Enumerable.Range(0, 30).Select(i => Candle(i % 4)).ToArray(); var expected = bars.Select((b, i) => b.Close - (i < 5 ? 0 : bars[i - 5].Close)).ToArray();
        using var context = new ComputeContext();
        using var scope = ComponentAverage.Arm((input, period) => { Assert.Equal(4, period); Assert.Equal(expected, input); return Enumerable.Repeat(0d, input.Count).ToArray(); });
        using var result = IndicatorCompute.ComputeEhlersDeviationScaledSuperSmootherFast(Data(bars), context, 5, length2: 7);
        Assert.All(result.ToArray(), v => Assert.True(double.IsFinite(v)));
        Assert.Equal(1, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions);
    }
    [Fact]
    public void InvalidFieldsLeaveMomentumRmsAndFeedbackUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersDeviationScaledSuperSmootherState(length1: 5, length2: 7); using var control = new EhlersDeviationScaledSuperSmootherState(length1: 5, length2: 7); state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 15).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
