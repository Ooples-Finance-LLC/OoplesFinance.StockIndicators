using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class StiffnessNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(StiffnessIndicator)).Select(c => new object[] { c });
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
    { var o = (StiffnessIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Si", BuiltInFormulaReferences.StiffnessOutputs(bars, o.Length1, o.Length2, o.SmoothingLength, Kind(o.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 100, int lookback = 60, int smoothing = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.StiffnessOutputs(bars, length, lookback, smoothing, Kind(kind)); var batch = Data(bars).CalculateStiffnessIndicator(kind, length, lookback, smoothing); Assert.Equal(expected, batch.OutputValues["Si"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeStiffnessIndicatorFast(Data(bars), context, length, lookback, smoothing, kind); Assert.Equal(expected, raw.ToArray());
        using var state = new StiffnessIndicatorState(kind, length, lookback, smoothing);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Si"]); }
            }
        }
    }
    [Fact]
    public void HandFullWindowDeviationStrictComparisonAndFixedVoteDenominator()
    {
        var bars = new[] { 1d, 2, 3 }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { 50d, 100, 100 }, BuiltInFormulaReferences.StiffnessOutputs(bars, 2, 2, 1)); Check(bars, 2, 2, 1);
        var flat = Enumerable.Repeat(Candle(1), 8).ToArray(); Assert.All(BuiltInFormulaReferences.StiffnessOutputs(flat, 1, 3, 1), v => Assert.Equal(0, v)); Check(flat, 1, 3, 1);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, 2, 3, 2, kind); Check(bars, int.MaxValue, int.MaxValue, int.MaxValue, kind); }
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void ExtremeAndShiftedPopulationsPreserveVotes()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 140).Select(i => Candle((i % 5 - 2) / 2d * scale)).ToArray(); Check(bars); Check(bars, 3, 7, 2, MovingAvgType.WeightedMovingAverage);
        }
        var shifted = Enumerable.Range(0, 35).Select(i => Candle(1e16 + (i % 4) * 2)).ToArray(); Check(shifted, 3, 5, 2);
        var boundary = new[] { 1d, 1, Math.BitIncrement(1), Math.BitDecrement(1), 1 }.Select(v => Candle(v)).ToArray(); Check(boundary, 2, 2, 1);
    }
    [Fact]
    public void SelectedSourceDrivesPricesAndThresholdDoesNotChangeOutput()
    {
        var selected = Enumerable.Range(0, 80).Select(i => i % 5 == 0 ? double.MaxValue : i % 5 == 1 ? -double.MaxValue : 1 + i % 7).ToArray(); var bars = selected.Select(_ => Candle(0)).ToArray(); var expected = BuiltInFormulaReferences.StiffnessOutputs(selected.Select(v => Candle(v)).ToArray()); Assert.Contains(expected, v => v != 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeStiffnessIndicatorFast(direct, context); Assert.Equal(expected, raw.ToArray());
        foreach (var threshold in new[] { -double.MaxValue, 0, 90, double.MaxValue })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.StiffnessIndicator, new StiffnessIndicatorSpecOptions(threshold: threshold)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateStiffnessIndicator(threshold: threshold).OutputValues["Si"]);
        }
    }
    [Fact]
    public void CustomerCallbacksKeepPriceAverageThenExponentialVoteSmoothing()
    {
        var bars = new[] { 1d, 2, 3, 4 }.Select(v => Candle(v)).ToArray();
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 1d, 2, 3, 4 }, input); return Enumerable.Repeat(3d, input.Count).ToArray(); },
                (input, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 0d, 0, 100d / 3, 200d / 3 }, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var output = Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Assert.Equal(output, Data(bars).CalculateStiffnessIndicator(MovingAvgType.WeightedMovingAverage, 2, 3, 2).OutputValues["Si"]);
            else { using var raw = IndicatorCompute.ComputeStiffnessIndicatorFast(Data(bars), context, 2, 3, 2, MovingAvgType.WeightedMovingAverage); Assert.Equal(output, raw.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new StiffnessIndicatorState(); using var control = new StiffnessIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
