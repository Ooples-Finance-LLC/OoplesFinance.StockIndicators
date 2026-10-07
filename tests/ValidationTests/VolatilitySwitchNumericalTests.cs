using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilitySwitchNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(VolatilitySwitchIndicator)).Select(c => new object[] { c });
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
    { var o = (VolatilitySwitchIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Vsi"] = BuiltInFormulaReferences.VolatilitySwitchOutputs(bars, o.Length, Kind(o.MaType)) }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 14, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var expected = BuiltInFormulaReferences.VolatilitySwitchOutputs(bars, length, Kind(kind));
        Assert.Equal(expected, Data(bars).CalculateVolatilitySwitchIndicator(kind, length).OutputValues["Vsi"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeVolatilitySwitchIndicatorFast(Data(bars), context, length, kind); Assert.Equal(expected, raw.ToArray());
        using var state = new VolatilitySwitchIndicatorState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Vsi"]); }
            }
        }
    }
    [Fact]
    public void HandSymmetricReturnsAndFullWindowStartup()
    {
        var bars = new[] { 1d, 3, 3, 1 }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { 0d, .25, .5, .5 }, BuiltInFormulaReferences.VolatilitySwitchOutputs(bars, 2, 1)); Check(bars, 2, MovingAvgType.SimpleMovingAverage);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, 1, kind); Check(bars, int.MaxValue, kind); }
        Check(new[] { 0d, 1, -1, 2, -2, 0, 1 }.Select(v => Candle(v)).ToArray(), 2); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void ExtremeMidpointsDifferencesAndCancellationRecover()
    {
        var extreme = new[] { double.MaxValue, double.MaxValue / 2, -double.MaxValue, Math.BitIncrement(-double.MaxValue), double.Epsilon, -double.Epsilon, 0, 2, 1, 2, 1 }.Select(v => Candle(v)).ToArray();
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(extreme, 2, kind);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 60).Select(i => Candle((i % 7 - 3) * scale)).ToArray(), 3);
        var ordinary = new[] { 1d, 2, 3, 1, 2, 4, 1 }.Select(v => Candle(v)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        Assert.Equal(BuiltInFormulaReferences.VolatilitySwitchOutputs(ordinary, 3), BuiltInFormulaReferences.VolatilitySwitchOutputs(scaled, 3));
    }
    [Fact]
    public void SelectedPricesDriveSymmetricReturns()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var expected = BuiltInFormulaReferences.VolatilitySwitchOutputs(bars.Select((b, i) => Candle(selected[i])).ToArray()); Assert.Contains(expected, v => v != 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeVolatilitySwitchIndicatorFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.VolatilitySwitchIndicator, new VolatilitySwitchIndicatorSpecOptions()), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateVolatilitySwitchIndicator().OutputValues["Vsi"]);
    }
    [Fact]
    public void CustomerCallbacksConsumeDeviationThenPrices()
    {
        var prices = new[] { 1d, 3, 3, 1 }; var bars = prices.Select(v => Candle(v)).ToArray();
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 0d, .5, .5, .5 }, input); return Enumerable.Repeat(42d, input.Count).ToArray(); },
                (input, period) => { Assert.Equal(2, period); Assert.Equal(prices, input); return Enumerable.Repeat(17d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var expected = Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Assert.Equal(expected, Data(bars).CalculateVolatilitySwitchIndicator(length: 2).OutputValues["Vsi"]);
            else { using var raw = IndicatorCompute.ComputeVolatilitySwitchIndicatorFast(Data(bars), context, 2); Assert.Equal(expected, raw.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions); Assert.Equal(2, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceReturnsOrAverages()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new VolatilitySwitchIndicatorState(); using var control = new VolatilitySwitchIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
