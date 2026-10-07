using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TrendExtractionNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersTrendExtraction)).Select(c => new object[] { c });
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
    { var o = (EhlersTrendExtractionSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.TrendExtractionValues(bars, o.Length, o.Delta, o.MaType); }
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage };
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 8, double delta = .1, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.TrendExtractionValues(bars, length, delta, kind); var batch = Data(bars).CalculateEhlersTrendExtraction(kind, length, delta); Assert.Equal(expected["Trend"], batch.CustomValuesList);
        using var context = new ComputeContext(); using var trend = IndicatorCompute.ComputeEhlersTrendExtractionFast(Data(bars), context, length, delta, kind); using var band = IndicatorCompute.ComputeEhlersTrendExtractionFast(Data(bars), context, length, delta, kind, true);
        Assert.Equal(expected["Trend"], trend.ToArray()); Assert.Equal(expected["Bp"], band.ToArray()); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var state = new EhlersTrendExtractionState(kind, length, delta);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(2)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Trend"][i], point.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideAndSubnormalPricesRetainBandAndTrend()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, 32 * double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue })
        {
            var values = Check(Enumerable.Range(0, 80).Select(i => Candle(i % 7 < 3 ? -scale : scale)).ToArray(), kind: kind);
            if (scale != double.Epsilon) { Assert.Contains(values["Bp"], v => v != 0); Assert.Contains(values["Trend"], v => v != 0); }
        }
    }
    [Fact]
    public void DoubledPeriodsUseLongArithmeticAndConsumedHistory()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 7 - 3)).ToArray();
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue / 2, int.MaxValue })
        {
            var values = Check(bars, length, kind: kind); Check(Array.Empty<Bar>(), length, kind: kind);
            if (length == int.MaxValue && kind == MovingAvgType.WeightedMovingAverage) Assert.Contains(values["Trend"], v => v != 0);
            if (length == int.MaxValue && kind == MovingAvgType.SimpleMovingAverage) Assert.All(values["Trend"], v => Assert.Equal(0, v));
        }
    }
    [Fact]
    public void BandStartsAfterTwoSamplesAndSimpleTrendWaitsForFullWindow()
    {
        var bars = new[] { 1d, 3, 5, -2, 4, 1, 7, -3 }.Select(v => Candle(v)).ToArray(); var values = Check(bars, 3);
        Assert.Equal(new[] { 0d, 0 }, values["Bp"].Take(2)); Assert.NotEqual(0, values["Bp"][2]); Assert.All(values["Trend"].Take(5), v => Assert.Equal(0, v)); Assert.NotEqual(0, values["Trend"][5]);
        foreach (var kind in Kinds) Assert.All(Check(Enumerable.Repeat(Candle(3), 30).ToArray(), 3, kind: kind)["Bp"], v => Assert.Equal(0, v));
    }
    [Fact]
    public void WidthClampsPreserveBothRecursionFeedbackTerms()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var kind in Kinds) foreach (var delta in new[] { -double.MaxValue, -1d, 0, .1, 1, double.MaxValue }) Check(bars, 8, delta, kind);
    }
    [Fact]
    public void InvalidCandlesDoNotAdvanceBandOrMean()
    {
        foreach (var kind in Kinds) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersTrendExtractionState(kind, 3); using var control = new EhlersTrendExtractionState(kind, 3);
            for (var i = 0; i < 8; i++) { state.Update(Native(Candle(i)), true, false); control.Update(Native(Candle(i)), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 15; i++) { var bar = Native(Candle(i % 4)); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Bp"], actual.Outputs!["Bp"]); }
        }
    }
    [Fact]
    public void SelectedPricesReachBothPublishedOutputs()
    {
        var selected = Enumerable.Range(0, 45).Select(i => (double)(i % 5 - 2)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var kind in Kinds)
        {
            var expected = BuiltInFormulaReferences.TrendExtractionValues(selected.Select(v => Candle(v)).ToArray(), 3, .1, kind); var data = Data(bars); data.SetCustomValues(selected); data.CalculateEhlersTrendExtraction(kind, 3); foreach (var key in expected.Keys) Assert.Equal(expected[key], data.OutputValues[key]);
            foreach (var band in new[] { false, true }) { data = Data(bars); data.SetCustomValues(selected); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersTrendExtractionFast(data, context, 3, .1, kind, band); Assert.Equal(expected[band ? "Bp" : "Trend"], result.ToArray()); }
        }
    }
    [Fact]
    public void ComponentAverageReceivesBandAtTwiceThePeriod()
    {
        var bars = new[] { 1d, 3, 5, -2 }.Select(v => Candle(v)).ToArray(); var band = BuiltInFormulaReferences.TrendExtractionValues(bars, 3, .1, Kinds[0])["Bp"];
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (input, period) => { calls++; Assert.Equal(6, period); Assert.Equal(band, input); return new[] { 11d, 12, 13, 14 }; } });
            if (fast) { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersTrendExtractionFast(Data(bars), context, 3); Assert.Equal(new[] { 11d, 12, 13, 14 }, result.ToArray()); }
            else Assert.Equal(new[] { 11d, 12, 13, 14 }, Data(bars).CalculateEhlersTrendExtraction(length: 3).CustomValuesList);
            Assert.Equal(1, calls);
        }
    }
    [Fact]
    public void FallbackAverageRetainsConfiguredSmoothing()
    {
        var bars = Enumerable.Range(0, 40).Select(i => Candle(i % 5 - 2)).ToArray(); var kind = MovingAvgType.ExponentialMovingAverage; var batch = Data(bars).CalculateEhlersTrendExtraction(kind, 3);
        using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeEhlersTrendExtractionFast(Data(bars), context, 3, .1, kind); Assert.Equal(batch.CustomValuesList, result.ToArray()); using var state = new EhlersTrendExtractionState(kind, 3);
        for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
    }
    [Fact]
    public void NonfiniteWidthsAreRejectedBeforeHistoryChanges()
    {
        foreach (var width in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersTrendExtractionState(delta: width));
    }
}
