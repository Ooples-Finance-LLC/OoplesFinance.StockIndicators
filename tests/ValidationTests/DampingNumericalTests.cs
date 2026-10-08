using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DampingNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("DI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double high, double low = 0, double close = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DampingIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLaggedRangeRatio(IndicatorValidationCase c, string route)
    {
        var options = (DampingIndexSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); var kind = options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 1;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Di", BuiltInFormulaReferences.DampingOutputs(bars, options.Length, kind) } }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.DampingOutputs(bars, length, k);
        Assert.Equal(expected, Data(bars).CalculateDampingIndex(kind, length).OutputValues["Di"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeDampingIndexFast(Data(bars), context, length, kind); Assert.Equal(expected, raw.ToArray());
        using var state = new DampingIndexState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue, -double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Di"]); }
            }
        }
    }
    [Fact]
    public void HandSixBarHistoryUsesPreviousRatherThanCurrentRange()
    {
        var bars = Enumerable.Range(1, 9).Select(i => Candle(i)).ToArray(); var expected = BuiltInFormulaReferences.DampingOutputs(bars, 1);
        Assert.Equal(new[] { 0d, 0, 0, 0, 0, 0, 6, 3.5, 8d / 3 }, expected); Check(bars, 1);
        Check(Enumerable.Repeat(Candle(0), 9).ToArray(), 1);
    }
    [Fact]
    public void ExtendedRangeAveragesAndPublishedOverflowRecover()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 5 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Enumerable.Range(0, 64).Select(i => Candle(i % 3 == 0 ? scale : 0, i % 3 == 1 ? -scale : 0)).ToArray(), length, kind);
            Check(Enumerable.Range(0, 64).Select(i => Candle(.5 + .4 * Math.Sin(i * .37), -.2)).ToArray(), length, kind);
            Check(Array.Empty<Bar>(), length, kind);
        }
        var recovery = Enumerable.Range(0, 24).Select(i => Candle(i == 0 ? double.Epsilon : i == 5 ? double.MaxValue : 1)).ToArray();
        var expected = BuiltInFormulaReferences.DampingOutputs(recovery, 1); Assert.True(double.IsPositiveInfinity(expected[6])); Assert.Equal(1, expected[^1]); Check(recovery, 1);
    }
    [Fact]
    public void SelectedPricesDoNotReplaceCandleRanges()
    {
        var bars = Enumerable.Range(0, 20).Select(i => Candle(1 + i % 5, -.5)).ToArray(); var selected = Enumerable.Range(0, 20).Select(i => i % 2 == 0 ? double.MaxValue : -double.MaxValue).ToArray();
        var expected = BuiltInFormulaReferences.DampingOutputs(bars, 2); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.DampingIndex, new DampingIndexSpecOptions(2)), context); Assert.NotNull(raw); Assert.Equal(expected, raw.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateDampingIndex(length: 2).OutputValues["Di"]);
    }
    [Fact]
    public void CustomerAveragesKeepPriceThenRangeOrder()
    {
        var bars = Enumerable.Range(0, 9).Select(i => Candle(i + 1, close: i + 2)).ToArray(); var prices = bars.Select(b => b.Close).ToArray(); var ranges = bars.Select(b => b.High - b.Low).ToArray();
        foreach (var batch in new[] { false, true })
        {
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] callbacks = {
                (input, period) => { Assert.Equal(2, period); Assert.Equal(prices, input); return Enumerable.Repeat(99d, input.Count).ToArray(); },
                (input, period) => { Assert.Equal(2, period); Assert.Equal(ranges, input); return Enumerable.Range(1, input.Count).Select(i => (double)i).ToArray(); }
            };
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext(); var expected = new[] { 0d, 0, 0, 0, 0, 0, 6, 3.5, 8d / 3 };
            if (batch) Assert.Equal(expected, Data(bars).CalculateDampingIndex(length: 2).OutputValues["Di"]);
            else { using var raw = IndicatorCompute.ComputeDampingIndexFast(Data(bars), context, 2); Assert.Equal(expected, raw.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceAnyRangeHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new DampingIndexState(length: 2); using var control = new DampingIndexState(length: 2);
            var seed = Native(Candle(2)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 16).Select(i => Candle(1 + i % 7))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
