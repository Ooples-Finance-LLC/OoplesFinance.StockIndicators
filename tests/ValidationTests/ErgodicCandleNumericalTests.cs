using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ErgodicCandleNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ECO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double open = 0, double high = 1, double low = 0) => new(DateTime.UnixEpoch, open, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ErgodicCandlestickOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentBodyAndRange(IndicatorValidationCase c, string route)
    {
        var options = (ErgodicCandlestickOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); var kind = options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 3;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ErgodicCandleOutputs(bars, 32, options.Length, kind), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int first, int second, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.ErgodicCandleOutputs(bars, first, second, k); var batch = Data(bars).CalculateErgodicCandlestickOscillator(kind, first, second);
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]); using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeErgodicCandlestickOscillatorFast(Data(bars), context, first, second, kind, key); Assert.Equal(expected[key], raw.ToArray());
        }
        using var state = new ErgodicCandlestickOscillatorState(kind, first, second);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Eco"][i], actual.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void HandBodyRangeAndSignalHaveIndependentValues()
    {
        var bars = new[] { Candle(1), Candle(0) }; var expected = BuiltInFormulaReferences.ErgodicCandleOutputs(bars, 1, 2);
        Assert.Equal(new[] { 100d, 50 }, expected["Eco"]); Assert.Equal(new[] { 100d, 75 }, expected["Signal"]); Check(bars, 1, 2);
        Check(new[] { Candle(2), Candle(-2), Candle(1, 1, 1, 1) }, 1, 1);
    }
    [Fact]
    public void ExtendedStagesRecoverWithoutClamping()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var periods in new[] { (1, 1), (2, 3), (32, 12) })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Enumerable.Range(0, 72).Select(i => Candle((i % 3 - 1) * scale, (1 - i % 3) * scale, scale, -scale)).ToArray(), periods.Item1, periods.Item2, kind);
            Check(Enumerable.Range(0, 72).Select(i => Candle(.5 + .4 * Math.Sin(i * .37), .3, .9, .1)).ToArray(), periods.Item1, periods.Item2, kind);
            Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, kind);
        }
        var recovery = Enumerable.Range(0, 40).Select(i => Candle(i == 0 ? double.MaxValue : 0)).ToArray();
        var expected = BuiltInFormulaReferences.ErgodicCandleOutputs(recovery, 1, 2);
        Assert.True(double.IsPositiveInfinity(expected["Eco"][0])); Assert.True(double.IsPositiveInfinity(expected["Signal"][0]));
        Assert.True(double.IsFinite(expected["Eco"][^1])); Assert.True(double.IsFinite(expected["Signal"][^1])); Check(recovery, 1, 2);
        Check(new[] { Candle(double.MaxValue, 0, double.Epsilon), Candle(0, 0, double.Epsilon) }, 1, 2);
    }
    [Fact]
    public void RawSelectedClosesRetainOpenAndRange()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 }; var original = selected.Select(_ => Candle(0, .5, 2, -1)).ToArray();
        var bars = selected.Select(v => Candle(v, .5, 2, -1)).ToArray(); var expected = BuiltInFormulaReferences.ErgodicCandleOutputs(bars, 32, 3);
        foreach (var key in expected.Keys)
        {
            var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.ErgodicCandlestickOscillator, new ErgodicCandlestickOscillatorSpecOptions(3), key), context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
        }
        var batch = Data(original); batch.SetCustomValues(selected.ToList()); batch.CalculateErgodicCandlestickOscillator(length2: 3);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
    }
    [Fact]
    public void CustomerStagesKeepFiveInputsAndPeriodsInOrder()
    {
        var bars = new[] { Candle(1), Candle(0) }; var inputs = new[] { new[] { 1d, 0 }, new[] { 1d, 1 }, new[] { 1d, 1 }, new[] { 4d, 4 }, new[] { 25d, 25 } };
        var outputs = new[] { 1d, 2, 4, 8, 7 }; var periods = new[] { 2, 3, 2, 3, 3 };
        foreach (var batch in new[] { false, true })
        {
            var callbacks = Enumerable.Range(0, 5).Select(index => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((input, period) => { Assert.Equal(periods[index], period); Assert.Equal(inputs[index], input); return Enumerable.Repeat(outputs[index], input.Count).ToArray(); })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            if (batch) { var result = Data(bars).CalculateErgodicCandlestickOscillator(length1: 2, length2: 3); Assert.Equal(new[] { 25d, 25 }, result.OutputValues["Eco"]); Assert.Equal(new[] { 7d, 7 }, result.OutputValues["Signal"]); }
            else { using var raw = IndicatorCompute.ComputeErgodicCandlestickOscillatorFast(Data(bars), context, 2, 3, key: "Signal"); Assert.Equal(new[] { 7d, 7 }, raw.ToArray()); }
            Assert.Equal(5, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceAnyStage()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new ErgodicCandlestickOscillatorState(length1: 2, length2: 3); using var control = new ErgodicCandlestickOscillatorState(length1: 2, length2: 3);
            var seed = Native(Candle(.2)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3)))
            {
                var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true);
                foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]);
            }
        }
    }
}
