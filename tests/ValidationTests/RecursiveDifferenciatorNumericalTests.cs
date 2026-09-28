using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RecursiveDifferenciatorNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RecursiveDifferenciator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRoundedRecurrence(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var options = (RecursiveDifferenciatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Rd", BuiltInFormulaReferences.RecursiveDifferenciatorTrajectory(bars, options.Length, options.Alpha, Kind(options.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(double[] prices, int length, double alpha, MovingAvgType kind)
    {
        var bars = prices.Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.RecursiveDifferenciatorTrajectory(bars, length, alpha, Kind(kind));
        Assert.Equal(expected, Data(bars).CalculateRecursiveDifferenciator(kind, length, alpha).OutputValues["Rd"]);
        using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeRecursiveDifferenciatorFast(Data(bars), context, length, alpha, kind);
        Assert.Equal(expected, output.ToArray());
        using var state = new RecursiveDifferenciatorState(kind, length, alpha);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Rd"]); }
            }
        }
    }
    [Fact]
    public void HandExampleFeedsBackTheDelayedChangeInsteadOfTheLevel()
    {
        var prices = new[] { 1d, 2, 1, 1, 2 };
        Assert.Equal(new[] { 1d, 1, 0, 0, .5 }, BuiltInFormulaReferences.RecursiveDifferenciatorTrajectory(prices.Select(v => Candle(v)).ToArray(), 1, .5, 1));
        Check(prices, 1, .5, MovingAvgType.SimpleMovingAverage);
    }
    [Fact]
    public void ExtremePricesAndPeriodsPreserveAverageStrengthAndFeedback()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 3, int.MaxValue })
        {
            var prices = Enumerable.Range(0, 32).Select(i => i < 12 ? (i % 2 == 0 ? scale : -scale) : 1d).ToArray();
            Check(prices, length, .6, kind); Check(Array.Empty<double>(), length, .6, kind);
        }
        foreach (var alpha in new[] { -1d, 0, 1, 2, 1e100, double.MaxValue })
            Check(new[] { 1d, 2, 3, 2, 1, 3, 2, 1 }, 2, alpha, MovingAvgType.ExponentialMovingAverage);
    }
    [Fact]
    public void DirectSelectedInputFeedsTheAverageBeforeStrength()
    {
        var prices = Enumerable.Range(0, 40).Select(i => (double)(i % 7)).ToArray();
        var bars = prices.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.RecursiveDifferenciatorTrajectory(prices.Select(v => Candle(v)).ToArray(), 3, .6, 3);
        var batch = Data(bars); batch.SetCustomValues(prices.ToList());
        Assert.Equal(expected, batch.CalculateRecursiveDifferenciator(length: 3).OutputValues["Rd"]);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeRecursiveDifferenciatorFast(data, context, 3);
        Assert.Equal(expected, output.ToArray());
    }
    [Fact]
    public void InvalidFieldsAndCoefficientsAreRejectedBeforeStateAdvances()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecursiveDifferenciatorState(alpha: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateRecursiveDifferenciator(alpha: invalid));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeRecursiveDifferenciatorFast(Data(Array.Empty<Bar>()), context, alpha: invalid));
        }
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new RecursiveDifferenciatorState(); using var control = new RecursiveDifferenciatorState();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
