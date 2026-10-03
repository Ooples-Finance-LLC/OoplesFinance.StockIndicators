using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MamaNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersMesaAdaptiveMovingAverage) || c.IndicatorType == typeof(FollowingAdaptiveMovingAverage)).Select(c => new object[] { c });
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
    {
        var options = ((IBuiltInIndicator)indicator).CreateOptions(); var mesa = options as EhlersMesaAdaptiveMovingAverageSpecOptions; var values = BuiltInFormulaReferences.MamaValues(bars.Select(b => b.Close).ToArray(), mesa?.FastLimit ?? .5, mesa?.SlowLimit ?? .05).Outputs;
        return indicator.Outputs.Count == 1 ? new() { { "Fama", values["Fama"] } } : values;
    }
    private static IndicatorCompute.EhlersMamaOutput Output(string key) => key switch { "Fama" => IndicatorCompute.EhlersMamaOutput.Fama, "I1" => IndicatorCompute.EhlersMamaOutput.InPhase, "Q1" => IndicatorCompute.EhlersMamaOutput.Quadrature, "SmoothPeriod" => IndicatorCompute.EhlersMamaOutput.SmoothPeriod, "Smooth" => IndicatorCompute.EhlersMamaOutput.Smooth, "Real" => IndicatorCompute.EhlersMamaOutput.Real, "Imag" => IndicatorCompute.EhlersMamaOutput.Imaginary, _ => IndicatorCompute.EhlersMamaOutput.Mama };
    private static Dictionary<string, double[]> Check(Bar[] bars, double fast = .5, double slow = .05)
    {
        var expected = BuiltInFormulaReferences.MamaValues(bars.Select(b => b.Close).ToArray(), fast, slow); var batch = Data(bars).CalculateEhlersMotherOfAdaptiveMovingAverages(fast, slow); Assert.Equal(expected.Outputs["Mama"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { using var result = IndicatorCompute.ComputeEhlersMotherOfAdaptiveMovingAveragesFast(Data(bars), context, fast, slow, Output(key)); Assert.Equal(expected.Outputs[key], result.ToArray()); Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); }
        using var state = new EhlersMotherOfAdaptiveMovingAveragesState(fast, slow);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in Enumerable.Range(0, 55).Select(i => Math.Sin(i * .3))) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Outputs["Mama"][i], point.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideAndSubnormalPricesPreserveEveryMamaTrajectory()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        { var values = Check(Enumerable.Range(0, 100).Select(i => Candle(Math.Sin(i * .27) * scale)).ToArray()); Assert.All(values["SmoothPeriod"], value => Assert.InRange(value, 0, 51)); }
    }
    [Fact]
    public void StartupAndSilenceRetainZeroInitialStateAndHalfRateFama()
    {
        var first = Check(new[] { Candle(1) }); Assert.Equal(.5, first["Mama"][0]); Assert.Equal(.125, first["Fama"][0]); Assert.Equal(.4, first["Smooth"][0]); Assert.Equal(0, first["I1"][0]); Assert.Equal(.33 * (.2 * 6), first["SmoothPeriod"][0]);
        var zeros = Check(Enumerable.Repeat(Candle(0), 90).ToArray()); foreach (var key in zeros.Keys.Where(k => k != "SmoothPeriod")) Assert.All(zeros[key], value => Assert.Equal(0, value)); Assert.True(zeros["SmoothPeriod"][89] > 0); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void IndependentAdaptiveGainsRetainFeedbackBeyondPublicationRange()
    {
        var bars = Enumerable.Range(0, 24).Select(i => Candle(i % 7 - 3)).ToArray(); foreach (var pair in new[] { (.2, .01), (.8, .05), (0d, 0d), (-.5, -.1), (2d, 1.5), (double.MaxValue, double.MaxValue) }) Check(bars, pair.Item1, pair.Item2);
    }
    [Fact]
    public void ExactPowerOfTwoScalingPreservesPeriodAndSignalOrdering()
    {
        var prices = Enumerable.Range(0, 100).Select(i => (double)(i % 9 - 4)).ToArray(); var baseline = Check(prices.Select(v => Candle(v)).ToArray()); var signals = Data(prices.Select(v => Candle(v)).ToArray()).CalculateEhlersMotherOfAdaptiveMovingAverages().SignalsList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) { var bars = prices.Select(v => Candle(v * scale)).ToArray(); var actual = Check(bars); Assert.Equal(baseline["SmoothPeriod"], actual["SmoothPeriod"]); Assert.Equal(signals, Data(bars).CalculateEhlersMotherOfAdaptiveMovingAverages().SignalsList); }
    }
    [Fact]
    public void SelectedPricesReachBatchAndEveryFastOutput()
    {
        var selected = Enumerable.Range(0, 80).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.MamaValues(selected.ToArray(), .5, .05); var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersMotherOfAdaptiveMovingAverages(); Assert.Equal(expected.Signals, data.SignalsList);
        foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersMotherOfAdaptiveMovingAveragesFast(source, context, .5, .05, Output(key)); Assert.Equal(expected.Outputs[key], result.ToArray()); }
        using var followerContext = new ComputeContext(); var follower = Data(bars); follower.SetCustomValues(selected); using var fama = IndicatorCompute.ComputeFollowingAdaptiveMovingAverageFast(follower, followerContext); Assert.Equal(expected.Outputs["Fama"], fama.ToArray());
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceHilbertOrAdaptiveHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersMotherOfAdaptiveMovingAveragesState(); using var control = new EhlersMotherOfAdaptiveMovingAveragesState();
            for (var i = 0; i < 50; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 30; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void NonfiniteAdaptiveGainsAreRejectedAtConstruction()
    { foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) { Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersMotherOfAdaptiveMovingAveragesState(fastAlpha: value)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersMotherOfAdaptiveMovingAveragesState(slowAlpha: value)); } }
}
