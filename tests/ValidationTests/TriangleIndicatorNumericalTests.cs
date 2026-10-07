using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TriangleIndicatorNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersTriangleWindowIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentIntegerWindow(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var options = (EhlersTriangleWindowIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.TriangleIndicatorOutputs(bars, options.Length, Kind(options.MaType)); }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.EhlersTriangleMovingAverage => 7, MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length, MovingAvgType kind)
    {
        var expected = BuiltInFormulaReferences.TriangleIndicatorOutputs(bars, length, Kind(kind));
        var batch = Data(bars).CalculateEhlersTriangleWindowIndicator(kind, length);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        { using var output = IndicatorCompute.ComputeEhlersTriangleWindowFast(Data(bars), context, length, kind, key); Assert.Equal(expected[key], output.ToArray()); }
        using var state = new EhlersTriangleWindowIndicatorState(kind, length);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), commit, true);
                    Assert.Equal(expected["Etwi"][i], actual.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void HandTriangleWeightsUseTheFullZeroPaddedWindow()
    {
        var bars = new[] { Candle(1), Candle(2), Candle(4) };
        var expected = BuiltInFormulaReferences.TriangleIndicatorOutputs(bars, 3, 7);
        Assert.Equal(new[] { .25, 1, 2.25 }, expected["Etwi"]);
        var factor = 1.5 * Math.PI;
        Assert.Equal(new[] { .25 * factor, .75 * factor, 1.25 * factor }, expected["Roc"]);
        Check(bars, 3, MovingAvgType.EhlersTriangleMovingAverage);
    }
    [Fact]
    public void WideDifferencesAndLaterRocCancellationRemainRepresentable()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var kind in new[] { MovingAvgType.EhlersTriangleMovingAverage, MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 3, 4, int.MaxValue })
        {
            var bars = Enumerable.Range(0, 32).Select(i => new Bar(DateTime.UnixEpoch, i < 12 ? -scale : 1, 4, 0, i < 12 ? scale : i % 3, 1)).ToArray();
            Check(bars, length, kind); Check(Array.Empty<Bar>(), length, kind);
        }
        var wide = Enumerable.Range(0, 3).Select(_ => new Bar(DateTime.UnixEpoch, -double.MaxValue, 4, 0, double.MaxValue, 1)).ToArray();
        var expected = BuiltInFormulaReferences.TriangleIndicatorOutputs(wide, 1, 7);
        Assert.All(expected["Etwi"], v => Assert.True(double.IsPositiveInfinity(v)));
        Assert.Equal(0, expected["Roc"][1]); Assert.Equal(0, expected["Roc"][2]);
        Check(wide, 1, MovingAvgType.EhlersTriangleMovingAverage);
    }
    [Fact]
    public void DirectSelectedInputRetainsOriginalOpenPrices()
    {
        var prices = Enumerable.Range(0, 40).Select(i => (double)(i % 7)).ToArray();
        var original = prices.Select((_, i) => new Bar(DateTime.UnixEpoch, i % 3, 4, 0, 100, 1)).ToArray();
        var projected = original.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.TriangleIndicatorOutputs(projected, 3, 7);
        var batch = Data(original); batch.SetCustomValues(prices.ToList()); batch.CalculateEhlersTriangleWindowIndicator(length: 3);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            var data = Data(original); data.SetCustomValues(prices.ToList());
            using var output = IndicatorCompute.ComputeEhlersTriangleWindowFast(data, context, 3, outputKey: key);
            Assert.Equal(expected[key], output.ToArray());
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceWindowOrPreviousFilteredValue()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersTriangleWindowIndicatorState(); using var control = new EhlersTriangleWindowIndicatorState();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
