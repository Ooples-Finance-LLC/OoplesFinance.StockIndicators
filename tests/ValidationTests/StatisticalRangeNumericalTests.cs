using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class StatisticalRangeNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = new IndicatorErrorBudget(0, 1e-9, true);
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(StatisticalVolatility)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLogRanges(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), new IndicatorErrorBudget(0, 1e-9, true));
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var options = (StatisticalVolatilitySpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.StatisticalRangeOutputs(bars, options.Length1, options.Length2, Kind(options.MaType)); }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 3, int annual = 5, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.StatisticalRangeOutputs(bars, length, annual, Kind(kind));
        var batch = Data(bars).CalculateStatisticalVolatility(kind, length, annual);
        foreach (var key in expected.Keys) Equal(expected[key], batch.OutputValues[key].ToArray());
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        { using var result = IndicatorCompute.ComputeStatisticalVolatilityFast(Data(bars), context, length, annual, kind, key); Equal(expected[key], result.ToArray()); }
        using var state = new StatisticalVolatilityState(kind, length, annual);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(double.MaxValue, double.MaxValue, double.Epsilon)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue, double.MaxValue, double.Epsilon)), false, false);
                foreach (var commit in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), commit, true);
                    foreach (var key in expected.Keys) Equal(new[] { expected[key][i] }, new[] { result.Outputs![key] });
                    Assert.Equal(result.Outputs!["Sv"], result.Value);
                }
            }
        }
    }
    [Fact]
    public void HandExampleHasIndependentRangeAnnualizationAndSignal()
    {
        var bars = new[] { Candle(2, 4, 2), Candle(4, 8, 4), Candle(2, 2, 2) };
        var expected = BuiltInFormulaReferences.StatisticalRangeOutputs(bars, 2, 2, 1);
        Equal(new[] { .3 * Math.Log(2), .9 * Math.Log(2), .9 * Math.Log(2) }, expected["Sv"]);
        Equal(new[] { 0d, .6 * Math.Log(2), .9 * Math.Log(2) }, expected["Signal"]);
        Check(bars, 2, 2, MovingAvgType.SimpleMovingAverage);
    }
    [Fact]
    public void ExtremeRatiosAdjacentPricesAndSignedDomainsRemainFinite()
    {
        var almost = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(double.MaxValue) - 1);
        var bars = new[] { Candle(almost, double.MaxValue, almost), Candle(double.MaxValue, double.MaxValue, almost),
            Candle(double.Epsilon, double.MaxValue, double.Epsilon), Candle(0, 0, 0), Candle(-2, -1, -4), Candle(-1, 4, -4),
            Candle(2, 4, 2), Candle(2, 2, 2), Candle(2, 2, 2), Candle(2, 2, 2) };
        var first = BuiltInFormulaReferences.StatisticalRangeOutputs(bars, 1, 1, 3)["Sv"][0];
        Assert.True(first > 0 && first < 1e-15);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var period in new[] { 1, 3, int.MaxValue })
        { Check(bars, period, int.MaxValue, kind); Check(Array.Empty<Bar>(), period, 1, kind); }
    }
    [Fact]
    public void CustomInputKeepsOriginalRangeAndExpiresOldExtremes()
    {
        var bars = Enumerable.Range(0, 24).Select(i => Candle(i % 2 == 0 ? 2 : 3, 8, 4)).ToArray();
        var expected = BuiltInFormulaReferences.StatisticalRangeOutputs(bars, 3, 5, 3);
        var original = bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 100, b.Volume)).ToArray();
        var batch = Data(original); batch.SetCustomValues(bars.Select(b => b.Close).ToList());
        batch.CalculateStatisticalVolatility(length1: 3, length2: 5);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Equal(expected[key], batch.OutputValues[key].ToArray());
            var data = Data(original); data.SetCustomValues(bars.Select(b => b.Close).ToList());
            using var output = IndicatorCompute.ComputeStatisticalVolatilityFast(data, context, 3, 5, outputKey: key);
            Equal(expected[key], output.ToArray());
        }
        using var state = new CustomInputState(new StatisticalVolatilityState(length1: 3, length2: 5), b => b.Close);
        for (var i = 0; i < bars.Length; i++) Equal(new[] { expected["Sv"][i] }, new[] { state.Update(Native(bars[i]), true, true).Value });
        Check(bars);
        var expiring = Enumerable.Range(0, 12).Select(i => Candle(i == 0 ? 100 : 2, i == 0 ? 100 : 2, 2)).ToArray();
        Check(expiring); Assert.Equal(0, BuiltInFormulaReferences.StatisticalRangeOutputs(expiring, 3, 5, 3)["Sv"][3]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceWindows()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new StatisticalVolatilityState(); using var control = new StatisticalVolatilityState();
            var seed = Native(Candle(2, 4, 1)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 8).Select(i => Native(Candle(i + 1, i + 2, 1))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
