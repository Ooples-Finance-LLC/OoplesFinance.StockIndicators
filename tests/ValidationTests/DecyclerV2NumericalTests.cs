using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DecyclerV2NumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersDecyclerOscillatorV2)).Select(c => new object[] { c });
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
    { var options = (EhlersDecyclerOscillatorV2SpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Edo", BuiltInFormulaReferences.DecyclerV2Trajectory(bars.Select(b => b.Close).ToArray(), options.FastLength, options.SlowLength, Kind(options.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(double[] prices, int fast, int slow, MovingAvgType kind)
    {
        var bars = prices.Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.DecyclerV2Trajectory(prices, fast, slow, Kind(kind));
        Assert.Equal(expected, Data(bars).CalculateEhlersDecyclerOscillatorV2(kind, fast, slow).OutputValues["Edo"]);
        using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeEhlersDecyclerOscillatorV2Fast(Data(bars), context, fast, kind, slow);
        Assert.Equal(expected, output.ToArray());
        using var state = new EhlersDecyclerOscillatorV2State(kind, fast, slow);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Edo"]); }
            }
        }
    }
    [Fact]
    public void EqualLegsCancelEvenWhenEachPublishedHighPassOverflows()
    {
        var impulse = new double[64]; impulse[4] = 1;
        var response = BuiltInFormulaReferences.HighPassV2Trajectory(impulse, 1, 2);
        var prices = new double[64];
        for (var i = 4; i < prices.Length; i++) prices[i] = Math.Sign(response[67 - i]) * double.MaxValue;
        Assert.True(double.IsPositiveInfinity(BuiltInFormulaReferences.HighPassV2Trajectory(prices, 1, 2)[^1]));
        Assert.All(BuiltInFormulaReferences.DecyclerV2Trajectory(prices, 1, 1, 2), value => Assert.Equal(0, value));
        Check(prices, 1, 1, MovingAvgType.WeightedMovingAverage);
    }
    [Fact]
    public void DifferentPeriodsRetainDirectionStartupAndRecovery()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var pair in new[] { (1, 3), (3, 7), (7, 3), (int.MaxValue, int.MaxValue) })
        {
            var prices = Enumerable.Range(0, 40).Select(i => i < 12 ? (i % 2 == 0 ? scale : -scale) : 1d).ToArray();
            Check(prices, pair.Item1, pair.Item2, kind); Check(Array.Empty<double>(), pair.Item1, pair.Item2, kind);
        }
        var impulse = new double[] { 0, 0, 0, 0, 1, 0, 0, 0 };
        var forward = BuiltInFormulaReferences.DecyclerV2Trajectory(impulse, 3, 7, 2);
        var reverse = BuiltInFormulaReferences.DecyclerV2Trajectory(impulse, 7, 3, 2);
        Assert.Equal(new[] { 0d, 0, 0, 0 }, forward.Take(4));
        Assert.Contains(forward, value => value != 0);
        Assert.Equal(forward.Select(value => -value), reverse);
    }
    [Fact]
    public void DirectSelectedInputFeedsBothLegs()
    {
        var prices = Enumerable.Range(0, 40).Select(i => (double)(i % 7)).ToArray();
        var bars = prices.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.DecyclerV2Trajectory(prices, 3, 7, 2);
        var batch = Data(bars); batch.SetCustomValues(prices.ToList());
        Assert.Equal(expected, batch.CalculateEhlersDecyclerOscillatorV2(fastLength: 3, slowLength: 7).OutputValues["Edo"]);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeEhlersDecyclerOscillatorV2Fast(data, context, 3, slowLength: 7);
        Assert.Equal(expected, output.ToArray());
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceEitherLeg()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersDecyclerOscillatorV2State(); using var control = new EhlersDecyclerOscillatorV2State();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
