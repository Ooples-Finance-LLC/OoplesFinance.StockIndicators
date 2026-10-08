using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VolatilityRatioNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(VolatilityRatio)).Select(c => new object[] { c });
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
    { var o = (VolatilityRatioSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { ["Vr"] = BuiltInFormulaReferences.VolatilityRatioOutputs(bars, o.Length) }; }
    private static void Check(Bar[] bars, int length = 14)
    {
        var expected = BuiltInFormulaReferences.VolatilityRatioOutputs(bars, length);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
            Assert.Equal(expected, Data(bars).CalculateVolatilityRatio(kind, length).OutputValues["Vr"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeVolatilityRatioFast(Data(bars), context, length); Assert.Equal(expected, raw.ToArray());
        using var state = new VolatilityRatioState(length: length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue, double.MaxValue, -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Vr"]); }
            }
        }
    }
    [Fact]
    public void HandPreviousRangeAndOlderPriceAlignment()
    {
        var bars = new[] { 1d, 3, 2, 5, 4 }.Select(v => Candle(v, v + 1, v - 1)).ToArray(); Assert.Equal(new[] { 0d, 1.5, 1, 2, 2d / 3 }, BuiltInFormulaReferences.VolatilityRatioOutputs(bars, 2)); Check(bars, 2);
        foreach (var length in new[] { 0, 1, int.MaxValue }) Check(bars, length); Check(Array.Empty<Bar>());
        Check(new[] { 0d, 2, 0, 3, 0, 4 }.Select(v => Candle(v, v, v)).ToArray(), 2);
    }
    [Fact]
    public void ExtendedTrueRangesAndDenominatorsRecover()
    {
        var extreme = new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, -double.MaxValue / 2, double.Epsilon, -double.Epsilon, 0, 2, 1 }.Select(v => Candle(v, double.MaxValue, -double.MaxValue)).ToArray();
        foreach (var length in new[] { 1, 2, 3, int.MaxValue }) Check(extreme, length);
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) Check(Enumerable.Range(0, 50).Select(i => Candle((i % 7 - 3) * scale, (i % 7 - 2) * scale, (i % 7 - 4) * scale)).ToArray(), 3);
        var ordinary = new[] { 1d, 2, 3, 1, 2, 4, 1 }.Select(v => Candle(v, v + 1, v - 1)).ToArray(); var scaleFactor = Math.ScaleB(1, 600); var scaled = ordinary.Select(b => Candle(b.Close * scaleFactor, b.High * scaleFactor, b.Low * scaleFactor)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.VolatilityRatioOutputs(ordinary, 3), BuiltInFormulaReferences.VolatilityRatioOutputs(scaled, 3));
    }
    [Fact]
    public void SelectedPricesKeepOriginalHighLowWindow()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2, i % 7 + 1, i % 3 - 2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var expected = BuiltInFormulaReferences.VolatilityRatioOutputs(bars.Select((b, i) => Candle(selected[i], b.High, b.Low)).ToArray()); Assert.Contains(expected, v => v != 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeVolatilityRatioFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.VolatilityRatio, new VolatilityRatioSpecOptions(14)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateVolatilityRatio().OutputValues["Vr"]);
    }
    [Fact]
    public void CustomerSignalAverageConsumesSelectedPrices()
    {
        var prices = new[] { 1d, 3, 2, 5, 4 }; var bars = prices.Select(v => Candle(v, v + 1, v - 1)).ToArray();
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(2, period); Assert.Equal(prices, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var expected = new[] { 0d, 1.5, 1, 2, 2d / 3 };
            if (batch) Assert.Equal(expected, Data(bars).CalculateVolatilityRatio(length: 2).OutputValues["Vr"]);
            else { using var raw = IndicatorCompute.ComputeVolatilityRatioFast(Data(bars), context, 2); Assert.Equal(expected, raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions); Assert.Equal(1, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceRangeOrPriceLag()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new VolatilityRatioState(); using var control = new VolatilityRatioState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
