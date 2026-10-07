using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HighPassV2NumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersHighPassFilterV2)).Select(c => new object[] { c });
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
    { var options = (EhlersHighPassFilterV2SpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Ehpf", BuiltInFormulaReferences.HighPassV2Trajectory(bars.Select(b => b.Close).ToArray(), options.Length, Kind(options.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(double[] prices, int length, MovingAvgType kind)
    {
        var bars = prices.Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.HighPassV2Trajectory(prices, length, Kind(kind));
        Assert.Equal(expected, Data(bars).CalculateEhlersHighPassFilterV2(kind, length).OutputValues["Ehpf"]);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeEhlersHighPassFilterV2Fast(Data(bars), context, length, kind);
        Assert.Equal(expected, fast.ToArray());
        if (kind == MovingAvgType.WeightedMovingAverage)
        { var core = new double[prices.Length]; MovingAverageCore.EhlersHighPassFilterV2(prices, core, length); Assert.Equal(expected, core); }
        using var state = new EhlersHighPassFilterV2State(kind, length);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Ehpf"]); }
            }
        }
    }
    [Fact]
    public void StartupAndTwoSmoothingStagesHaveHandValues()
    {
        var prices = new[] { 0d, 0, 0, 0, 1, 0, 0, 0 };
        var angle = Math.Sqrt(2) * Math.PI / 2; var decay = Math.Exp(-angle);
        var gain = (1 + 2 * decay * Math.Cos(angle) + decay * decay) / 4;
        var expected = BuiltInFormulaReferences.HighPassV2Trajectory(prices, 2, 1);
        Assert.Equal(new[] { 0d, 0, 0, 0 }, expected.Take(4)); Assert.Equal(gain / 4, expected[4]);
        Check(prices, 2, MovingAvgType.SimpleMovingAverage);
    }
    [Fact]
    public void IntermediateOverflowDoesNotPoisonLaterFiniteOutputs()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 3, 20, int.MaxValue })
        {
            var prices = Enumerable.Range(0, 48).Select(i => i < 12 ? (i % 2 == 0 ? scale : -scale) : 1d).ToArray();
            Check(prices, length, kind); Check(Array.Empty<double>(), length, kind);
        }
        var constant = Enumerable.Repeat(double.MaxValue, 24).ToArray();
        Check(constant, 3, MovingAvgType.WeightedMovingAverage);
        Assert.All(BuiltInFormulaReferences.HighPassV2Trajectory(constant, 3, 2), v => Assert.Equal(0, v));
    }
    [Fact]
    public void SharedDecyclerStillMatchesBothComponentPeriods()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(10 + i % 7)).ToArray();
        var slow = BuiltInFormulaReferences.HighPassV2Trajectory(bars.Select(b => b.Close).ToArray(), 7, 2);
        var fast = BuiltInFormulaReferences.HighPassV2Trajectory(bars.Select(b => b.Close).ToArray(), 3, 2);
        var expected = slow.Zip(fast, (a, b) => a - b).ToArray();
        Assert.Equal(expected, Data(bars).CalculateEhlersDecyclerOscillatorV2(fastLength: 3, slowLength: 7).OutputValues["Edo"]);
        using var state = new EhlersDecyclerOscillatorV2State(fastLength: 3, slowLength: 7);
        for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceRecursiveStages()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersHighPassFilterV2State(); using var control = new EhlersHighPassFilterV2State();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
    [Fact]
    public void DirectSelectedInputDiffersFromTheOriginalCloseColumn()
    {
        var prices = Enumerable.Range(0, 40).Select(i => (double)(i % 7)).ToArray();
        var bars = prices.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.HighPassV2Trajectory(prices, 3, 2);
        var batch = Data(bars); batch.SetCustomValues(prices.ToList());
        Assert.Equal(expected, batch.CalculateEhlersHighPassFilterV2(length: 3).OutputValues["Ehpf"]);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeEhlersHighPassFilterV2Fast(data, context, 3);
        Assert.Equal(expected, output.ToArray());
    }

}
