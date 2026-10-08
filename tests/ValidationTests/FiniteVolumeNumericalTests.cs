using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FiniteVolumeNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FiniteVolumeElements)).Select(c => new object[] { c });
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
    { var o = (FiniteVolumeElementsSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Fve", BuiltInFormulaReferences.FiniteVolumeOutputs(bars, o.Length, o.Factor, Kind(o.MaType)) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 22, double factor = .3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.FiniteVolumeOutputs(bars, length, factor, Kind(kind)); var batch = Data(bars).CalculateFiniteVolumeElements(kind, length, factor); Assert.Equal(expected, batch.OutputValues["Fve"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeFiniteVolumeElementsFast(Data(bars), context, length, factor, kind); Assert.Equal(expected, raw.ToArray());
        using var state = new FiniteVolumeElementsState(kind, length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Fve"]); }
            }
        }
    }
    [Fact]
    public void HandSignedVolumeAndZeroAverageHold()
    {
        var bars = new[] { 1d, 2, 1, 1 }.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray(); Assert.Equal(new[] { 100d, 200, 100, 100 }, BuiltInFormulaReferences.FiniteVolumeOutputs(bars, 1)); Check(bars, 1);
        var zeroVolume = new[] { 1d, 2, 1, 0 }.Select((v, i) => new Bar(DateTime.UnixEpoch, v, v, v, v, i == 1 || i == 2 ? 0 : 1)).ToArray(); Assert.Equal(new[] { 100d, 100, 100, 0 }, BuiltInFormulaReferences.FiniteVolumeOutputs(zeroVolume, 1)); Check(zeroVolume, 1);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, 2, .3, kind); Check(bars, int.MaxValue, .3, kind); }
        foreach (var factor in new[] { -3d, 0, .3, 100, double.MaxValue, -double.MaxValue, double.Epsilon }) Check(bars, 2, factor);
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void ExtremePricesVolumesAndThresholdsRemainComparable()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 100).Select(i => new Bar(DateTime.UnixEpoch, 0, scale, -scale, (i % 5 - 2) / 2d * scale, i % 3 == 0 ? 0 : scale)).ToArray(); Check(bars); Check(bars, 3, .3, MovingAvgType.WeightedMovingAverage); Check(bars, 2, double.MaxValue);
        }
        var prices = new[] { 1d, 2, 1, 2 }.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, double.MaxValue)).ToArray(); Assert.Equal(new[] { 100d, 200, 100, 200 }, BuiltInFormulaReferences.FiniteVolumeOutputs(prices, 1)); Check(prices, 1);
    }
    [Fact]
    public void SelectedCloseRetainsOriginalCandleRangeAndVolumes()
    {
        var bars = Enumerable.Range(0, 80).Select(i => new Bar(DateTime.UnixEpoch, 1, 4 + i % 3, -i % 2, 2, 1 + i % 5)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => i % 5 == 0 ? double.MaxValue : i % 5 == 1 ? -double.MaxValue : 1 + i % 7).ToArray();
        var expected = BuiltInFormulaReferences.FiniteVolumeOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray()); Assert.Contains(expected, v => v != 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeFiniteVolumeElementsFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.FiniteVolumeElements, new FiniteVolumeElementsSpecOptions()), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateFiniteVolumeElements().OutputValues["Fve"]);
    }
    [Fact]
    public void CustomerCallbackReceivesVolumeAndConfiguredPeriod()
    {
        var bars = new[] { 1d, 2, 1, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch, v, v, v, v, i + 1)).ToArray();
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 1d, 2, 3, 4 }, input); return Enumerable.Repeat(2d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var output = new[] { 25d, 75, 0, 100 };
            if (batch) Assert.Equal(output, Data(bars).CalculateFiniteVolumeElements(length: 2).OutputValues["Fve"]);
            else { using var raw = IndicatorCompute.ComputeFiniteVolumeElementsFast(Data(bars), context, 2); Assert.Equal(output, raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void NonfiniteFactorsAreRejected()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var bars = new[] { Candle(1) }; Assert.Throws<ArgumentOutOfRangeException>(() => new FiniteVolumeElementsState(factor: value)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateFiniteVolumeElements(factor: value));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeFiniteVolumeElementsFast(Data(bars), context, factor: value));
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new FiniteVolumeElementsState(); using var control = new FiniteVolumeElementsState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
