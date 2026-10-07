using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MovementStrengthNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(StrengthOfMovement)).Select(c => new object[] { c });
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
    { var o = (StrengthOfMovementSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Som", BuiltInFormulaReferences.MovementStrengthOutputs(bars, o.Length1, o.Length2, 3, Kind(o.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 10, int movement = 3, int smoothing = 3, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var expected = BuiltInFormulaReferences.MovementStrengthOutputs(bars, length, movement, smoothing, Kind(kind)); var batch = Data(bars).CalculateStrengthOfMovement(kind, length, movement, smoothing); Assert.Equal(expected, batch.OutputValues["Som"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeStrengthOfMovementFast(Data(bars), context, length, movement, kind, smoothing); Assert.Equal(expected, raw.ToArray());
        using var state = new StrengthOfMovementState(kind, length, movement, smoothing);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Som"]); Assert.InRange(actual.Value, -100d, 100d); }
            }
        }
    }
    [Fact]
    public void HandMovementFlatRangeAndMinimumTwoBarExtrema()
    {
        var bars = new[] { 1d, 2, 4, 2 }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { -100d, 100, -100, -100 }, BuiltInFormulaReferences.MovementStrengthOutputs(bars, 1, 2, 1)); Check(bars, 1, 2, 1);
        Assert.All(BuiltInFormulaReferences.MovementStrengthOutputs(bars, 1, 1, 1), v => Assert.Equal(-100, v)); Check(bars, 1, 1, 1);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, int.MaxValue, 2, int.MaxValue, kind); Check(bars, 1, int.MaxValue, 1, kind); Check(bars, 2, 3, 2, kind); }
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void ExtendedReturnsAndDifferencesNormalizeBeforePublication()
    {
        var extreme = new[] { double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, double.MaxValue, 1, 0, 2, 1 }.Select(v => Candle(v)).ToArray();
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(extreme, 3, 2, 2, kind);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 60).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 3, 3, 2);
    }
    [Fact]
    public void SelectedPricesDriveReturnsAndOwnRange()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var expected = BuiltInFormulaReferences.MovementStrengthOutputs(bars.Select((b, i) => Candle(selected[i])).ToArray()); Assert.Contains(expected, v => v > 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeStrengthOfMovementFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.StrengthOfMovement, new StrengthOfMovementSpecOptions()), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateStrengthOfMovement().OutputValues["Som"]);
    }
    [Fact]
    public void CustomerCallbacksRetainReturnsCenteringAndPeriods()
    {
        var bars = new[] { 1d, 2, 4, 2 }.Select(v => Candle(v)).ToArray();
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 0d, 1, 1, -.5 }, input); return new[] { 0d, 1, 2, 1 }; },
                (input, period) => { Assert.Equal(3, period); Assert.Equal(new[] { -100d, 100, 100, -100 }, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var expected = Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Assert.Equal(expected, Data(bars).CalculateStrengthOfMovement(length1: 2, length2: 2, smoothingLength: 3).OutputValues["Som"]);
            else { using var raw = IndicatorCompute.ComputeStrengthOfMovementFast(Data(bars), context, 2, 2, smoothingLength: 3); Assert.Equal(expected, raw.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new StrengthOfMovementState(); using var control = new StrengthOfMovementState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
