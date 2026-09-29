using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RoofAutocorrelationNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = -4) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar {i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAutoCorrelationIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCenteredCovariance(IndicatorValidationCase c, string route)
    {
        var options = (EhlersAutoCorrelationIndicatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.RoofAutocorrelationValues(bars, options.Length1, options.Length2).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length = 7, int smoothing = 3)
    {
        var expected = BuiltInFormulaReferences.RoofAutocorrelationValues(bars, length, smoothing); var values = expected.Outputs["Eaci"]; var batch = Data(bars).CalculateEhlersAutoCorrelationIndicator(length, smoothing); Assert.Equal(new[] { "Eaci" }, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(values, batch.CustomValuesList); Equal(values, batch.OutputValues["Eaci"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersAutoCorrelationIndicatorFast(Data(bars), context, length, smoothing); Equal(values, fast.ToArray()); var core = new double[bars.Length]; OscillatorCore.EhlersAutoCorrelationIndicator(bars.Select(b => b.Close).ToArray(), core, length, smoothing); Equal(values, core);
        using var state = new EhlersAutoCorrelationIndicatorState(length, smoothing);
        for (var replay = 0; replay < 2; replay++)
        {
            for (var i = 0; i < 30; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), commit, true); Assert.True(IndicatorErrorBudget.Exact.Accepts(values[i], actual.Value)); Assert.Equal(actual.Value, actual.Outputs!["Eaci"]); } }
        }
        return values;
    }
    [Fact]
    public void WideAndSubnormalPricesRetainCenteredCovarianceAndSignals()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) foreach (var periods in new[] { (3, 1), (7, 3), (31, 10) })
        { var values = Check(Enumerable.Range(0, 95).Select(i => Candle((Math.Sin(i * .27) + .1 * Math.Cos(i * .71)) * .8 * scale)).ToArray(), periods.Item1, periods.Item2); Assert.All(values, value => Assert.InRange(value, 0, 1)); }
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsPreserveLaggedStartup()
    {
        Check(Array.Empty<Bar>()); Assert.All(Check(Enumerable.Repeat(Candle(0), 80).ToArray()), value => Assert.Equal(0, value));
        foreach (var periods in new[] { (int.MinValue, 0), (0, 1), (1, 1), (31, 3), (int.MaxValue, 7), (7, int.MaxValue) })
        { var values = Check(Enumerable.Range(0, 80).Select(i => Candle(Math.Sin(i * .3))).ToArray(), periods.Item1, periods.Item2); Assert.All(values.Take(Math.Min(values.Length, Math.Max(1, periods.Item1))), value => Assert.Equal(0, value)); }
        Assert.All(Check(Enumerable.Range(0, 80).Select(i => Candle(i)).ToArray(), 1), value => Assert.Equal(0, value));
    }
    [Fact]
    public void ReflectionAndPowerOfTwoScalingPreserveNormalizedCorrelation()
    {
        var prices = Enumerable.Range(0, 100).Select(i => Math.Sin(i * .25) + .25 * Math.Cos(i * .71)).ToArray(); var expected = Check(prices.Select(v => Candle(v)).ToArray(), 9);
        foreach (var scale in new[] { -1d, Math.Pow(2, -500), Math.Pow(2, 500) }) Equal(expected, Check(prices.Select(v => Candle(v * scale)).ToArray(), 9));
    }
    [Fact]
    public void MixedExponentHistoriesRemainFiniteThroughWindowExpiry()
    {
        var bars = Enumerable.Range(0, 120).Select(i => Candle(i < 40 ? Math.Sin(i * .37) * double.MaxValue : i < 80 ? Math.Sin(i * .21) * Math.Pow(2, -1000) : Math.Sin(i * .47))).ToArray(); Assert.All(Check(bars, 5, 1), value => Assert.InRange(value, 0, 1));
    }
    [Fact]
    public void SelectedPricesReachBatchAndFast()
    {
        var selected = Enumerable.Range(0, 85).Select(i => Math.Sin(i * .3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.RoofAutocorrelationValues(selected.Select(v => Candle(v)).ToArray(), 7, 3);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersAutoCorrelationIndicator(7, 3); Equal(expected.Outputs["Eaci"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersAutoCorrelationIndicatorFast(source, context, 7, 3); Equal(expected.Outputs["Eaci"], result.ToArray());
    }
    [Fact]
    public void CoreRejectsShortOutputBeforeChangingIt()
    {
        var target = new[] { 123d }; Assert.Throws<ArgumentException>(() => OscillatorCore.EhlersAutoCorrelationIndicator(new[] { 1d, 2 }, target)); Assert.Equal(123, target[0]);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRoofOrLaggedHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersAutoCorrelationIndicatorState(7, 3); using var control = new EhlersAutoCorrelationIndicatorState(7, 3);
            for (var i = 0; i < 25; i++) { var bar = Native(Candle(Math.Sin(i * .3))); state.Update(bar, true, false); control.Update(bar, true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 20; i++) { var bar = Native(Candle(Math.Sin(i * .4))); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Eaci"], actual.Outputs!["Eaci"]); }
        }
    }
}
