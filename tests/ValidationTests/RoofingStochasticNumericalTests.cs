using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RoofingStochasticNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersModifiedStochasticIndicator) || c.IndicatorType == typeof(EhlersStochastic)).Select(c => new object[] { c });
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
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, MovingAvgType.WeightedMovingAverage };
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    {
        var options = ((IBuiltInIndicator)indicator).CreateOptions();
        if (options is EhlersModifiedStochasticIndicatorSpecOptions m) return new() { { "Emsi", BuiltInFormulaReferences.RoofingStochasticValues(bars, m.Length1, m.Length2, m.Length3, m.MaType, true) } };
        var o = (EhlersStochasticSpecOptions)options; return new() { { "Es", BuiltInFormulaReferences.RoofingStochasticValues(bars, 48, 10, o.Length, o.MaType, false) } };
    }
    private static StockData Batch(Bar[] bars, bool modified, int high, int low, int length, MovingAvgType kind)
        => Apply(Data(bars), modified, high, low, length, kind);
    private static StockData Apply(StockData data, bool modified, int high, int low, int length, MovingAvgType kind)
        => modified ? data.CalculateEhlersModifiedStochasticIndicator(kind, high, low, length) : data.CalculateEhlersStochastic(kind, high, length, low);
    private static ComputeBuffer Fast(StockData data, ComputeContext context, bool modified, int high, int low, int length, MovingAvgType kind)
        => modified ? IndicatorCompute.ComputeEhlersModifiedStochasticFast(data, context, high, low, length, kind) : IndicatorCompute.ComputeEhlersStochasticFast(data, context, high, length, low, kind);
    private static IStreamingIndicatorState State(bool modified, int high, int low, int length, MovingAvgType kind)
        => modified ? new EhlersModifiedStochasticIndicatorState(kind, high, low, length) : new EhlersStochasticState(kind, high, length, low);
    private static double[] Check(Bar[] bars, bool modified, int high = 48, int low = 10, int length = 20, MovingAvgType kind = MovingAvgType.Ehlers2PoleSuperSmootherFilterV1)
    {
        var expected = BuiltInFormulaReferences.RoofingStochasticValues(bars, high, low, length, kind, modified); var batch = Batch(bars, modified, high, low, length, kind); var key = modified ? "Emsi" : "Es"; Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues[key]);
        using var context = new ComputeContext(); using var fast = Fast(Data(bars), context, modified, high, low, length, kind); Assert.Equal(expected, fast.ToArray());
        var state = State(modified, high, low, length, kind); using var lifetime = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(-double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], point.Value); Assert.Equal(expected[i], point.Outputs![key]); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideAndTinyRoofingValuesRetainWindowRanks()
    {
        foreach (var modified in new[] { false, true }) foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue })
        {
            var values = Check(Enumerable.Range(0, 80).Select(i => Candle(i % 3 == 0 ? -scale : scale)).ToArray(), modified, 48, 8, 7, kind); Assert.Contains(values, v => v != 0);
        }
    }
    [Fact]
    public void PowerOfTwoScalingPreservesRankAndOutput()
    {
        var prices = Enumerable.Range(0, 80).Select(i => (double)(i % 7 - 3)).ToArray();
        foreach (var modified in new[] { false, true }) foreach (var kind in Kinds)
        {
            var baseline = Check(prices.Select(v => Candle(v)).ToArray(), modified, kind: kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) }) Assert.Equal(baseline, Check(prices.Select(v => Candle(v * scale)).ToArray(), modified, kind: kind));
        }
    }
    [Fact]
    public void ZeroRangeAndNyquistCutoffStayZeroFromStartup()
    {
        foreach (var modified in new[] { false, true }) foreach (var kind in Kinds)
        {
            Assert.All(Check(Enumerable.Repeat(Candle(0), 50).ToArray(), modified, kind: kind), v => Assert.Equal(0, v));
            Assert.All(Check(Enumerable.Range(0, 50).Select(i => Candle(i)).ToArray(), modified, 1, 2, 1, kind), v => Assert.Equal(0, v));
            Check(new[] { 3d, 1, 4, -2, 0, 7 }.Select(v => Candle(v)).ToArray(), modified, 8, 2, 3, kind);
            // The third roofing sample is inside its range, so premature recursion changes the rank.
            Check(new[] { 1d, 3, 2 }.Select(v => Candle(v)).ToArray(), modified, 8, 5, 3, kind);
        }
    }
    [Fact]
    public void ExtremePeriodsUseConsumedHistoryAndRetainFilterGain()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var modified in new[] { false, true }) foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) { Check(bars, modified, period, period, period, kind); Check(Array.Empty<Bar>(), modified, period, period, period, kind); }
        Assert.Contains(Check(bars, true, int.MaxValue, 8, 3), v => v != 0);
    }
    [Fact]
    public void MinimumRankWindowIncludesTwoObservations()
    {
        foreach (var modified in new[] { false, true })
        {
            using var window = new RoofingStochasticWindow(Kinds[0], 48, 10, 1, modified, true); var unit = modified ? 100d : 1d;
            Assert.Equal(new[] { 0d, unit, 0d, unit }, new[] { 2d, 3, 1, 2 }.Select(v => window.Rank(v, true)).ToArray());
        }
    }
    [Fact]
    public void TiedExtremaExpireAndPreviewsDoNotAlterDequeOrder()
    {
        foreach (var modified in new[] { false, true })
        {
            using var window = new RoofingStochasticWindow(Kinds[0], 48, 10, 3, modified, true); var input = new[] { 5d, 1, 1, 4, 4, 2, 6 }; var expected = new[] { 0d, 0, 0, 1, 1, 0, 1 }.Select(v => v * (modified ? 100 : 1)).ToArray();
            for (var replay = 0; replay < 2; replay++) { window.Reset(); for (var i = 0; i < input.Length; i++) { window.Rank(99, false); Assert.Equal(expected[i], window.Rank(input[i], false)); Assert.Equal(expected[i], window.Rank(input[i], true)); } }
        }
    }
    [Fact]
    public void BelowRangeRoofingTailsStillReachTheUpperRank()
    {
        foreach (var modified in new[] { false, true }) foreach (var scale in new[] { 1d, double.Epsilon })
        {
            var result = Check(Enumerable.Range(0, 300).Select(i => Candle((i % 2 == 0 ? 2 : 3) * scale)).ToArray(), modified, 24, 5, 10);
            var target = modified ? 100d : 1d; Assert.All(result.Skip(250), v => Assert.InRange(v, target - 1e-9, target + 1e-9));
        }
    }
    [Fact]
    public void SelectedPricesReachBothBatchAndFastPaths()
    {
        var selected = Enumerable.Range(0, 65).Select(i => (double)(i % 7 - 3)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var modified in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.RoofingStochasticValues(selected.Select(v => Candle(v)).ToArray(), 8, 5, 3, Kinds[0], modified); var data = Data(bars); data.SetCustomValues(selected); Apply(data, modified, 8, 5, 3, Kinds[0]); Assert.Equal(expected, data.CustomValuesList);
            using var context = new ComputeContext(); data = Data(bars); data.SetCustomValues(selected); using var fast = Fast(data, context, modified, 8, 5, 3, Kinds[0]); Assert.Equal(expected, fast.ToArray());
        }
    }
    [Fact]
    public void InvalidFieldsLeaveRoofingRangeAndOutputUnchanged()
    {
        foreach (var modified in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = State(modified, 48, 10, 7, Kinds[0]); var control = State(modified, 48, 10, 7, Kinds[0]); using var lifetime = (IDisposable)state; using var other = (IDisposable)control; state.Update(Native(Candle(2)), true, false); control.Update(Native(Candle(2)), true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 25).Select(i => Native(Candle(i % 3 - 1)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
    [Fact]
    public void ComponentCallbacksReceiveRoofingThenPairedRanks()
    {
        var bars = new[] { 1d, 3, 2, 6 }.Select(v => Candle(v)).ToArray();
        foreach (var modified in new[] { false, true }) foreach (var fast in new[] { false, true })
        {
            var calls = 0; var callbacks = new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>();
            callbacks.Add((values, period) => { calls++; Assert.Equal(5, period); Assert.Equal(4, values.Count); Assert.All(values, v => Assert.True(double.IsFinite(v))); return new[] { 1d, 3, 2, 4 }; });
            if (!modified) callbacks.Add((values, period) => { calls++; Assert.Equal(3, period); Assert.Equal(new[] { 0d, .5, .75, .75 }, values); return new[] { 11d, 12, 13, 14 }; });
            using var armed = ComponentAverage.Arm(callbacks.ToArray()); double[] actual;
            if (fast) { using var context = new ComputeContext(); using var result = Fast(Data(bars), context, modified, 8, 5, 3, Kinds[0]); actual = result.ToArray(); }
            else actual = Batch(bars, modified, 8, 5, 3, Kinds[0]).CustomValuesList.ToArray();
            if (!modified) Assert.Equal(new[] { 11d, 12, 13, 14 }, actual);
            else
            {
                var radius = Math.Exp(-Math.Sqrt(2) * Math.PI / 8); var b = 2 * radius * Math.Cos(Math.Sqrt(2) * Math.PI / 8); var c = -radius * radius; var gain = 1 - b - c; var expected = new double[4]; var pairs = new[] { 0d, 50, 75, 75 };
                for (var i = 0; i < 4; i++) { expected[i] = gain * pairs[i] + (i == 0 ? 0 : b * expected[i - 1]) + (i < 2 ? 0 : c * expected[i - 2]); Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, 1e-10); }
            }
            Assert.Equal(modified ? 1 : 2, calls);
        }
    }
    [Fact]
    public void OtherAverageKindsRetainConfiguredSmoothing()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var modified in new[] { false, true })
        {
            var batch = Batch(bars, modified, 48, 5, 3, MovingAvgType.SimpleMovingAverage); var state = State(modified, 48, 5, 3, MovingAvgType.SimpleMovingAverage); using var lifetime = (IDisposable)state;
            using var context = new ComputeContext(); using var fast = Fast(Data(bars), context, modified, 48, 5, 3, MovingAvgType.SimpleMovingAverage); Assert.Equal(batch.CustomValuesList, fast.ToArray());
            for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
        }
    }
}
