using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChartmillNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CMV", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double open = 0, double high = 2, double low = 0) => new(DateTime.UnixEpoch, open, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChartmillValueIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCenterAndTrueRange(IndicatorValidationCase c, string route)
    {
        var options = (ChartmillValueIndicatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); var kind = options.MaType == MovingAvgType.WeightedMovingAverage ? 2 : 1;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ChartmillOutputs(bars, options.Length, kind), IndicatorErrorBudget.Exact);
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
        var expected = BuiltInFormulaReferences.ChartmillOutputs(bars, length, k); var batch = Data(bars).CalculateChartmillValueIndicator(kind, length);
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]); using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeChartmillValueIndicatorFast(Data(bars), context, length, kind, key); Assert.Equal(expected[key], raw.ToArray());
        }
        using var state = new ChartmillValueIndicatorState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue, 0, double.MaxValue, -double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue, 0, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Cmvc"][i], actual.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void HandCenterRangeAndFourPricesRemainDistinct()
    {
        var bars = new[] { Candle(3, 2, 4, 0) }; var expected = BuiltInFormulaReferences.ChartmillOutputs(bars, 1);
        Assert.Equal(new[] { .25 }, expected["Cmvc"]); Assert.Equal(new[] { 0d }, expected["Cmvo"]); Assert.Equal(new[] { .5 }, expected["Cmvh"]); Assert.Equal(new[] { -.5 }, expected["Cmvl"]); Check(bars, 1);
        Check(new[] { Candle(1, 1, 1, 1), Candle(1, 1, 1, 1) }, 1);
    }
    [Fact]
    public void ExtremeDifferencesAndRangeProductsRecover()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 5 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Enumerable.Range(0, 64).Select(i => Candle((i % 3 - 1) * scale, (1 - i % 3) * scale, scale, -scale)).ToArray(), length, kind);
            Check(Enumerable.Range(0, 64).Select(i => Candle(.5 + .4 * Math.Sin(i * .37), .25, .9, .1)).ToArray(), length, kind);
            Check(new[] { Candle(0, 0, double.Epsilon), Candle(double.MaxValue, -double.MaxValue, double.Epsilon), Candle(0, 0, double.Epsilon) }, length, kind);
            Check(Array.Empty<Bar>(), length, kind);
        }
        var recovery = Enumerable.Range(0, 30).Select(i => i < 3 ? Candle(double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue) : Candle(3, 2, 4, 0)).ToArray();
        var expected = BuiltInFormulaReferences.ChartmillOutputs(recovery, 2); Assert.True(expected["Cmvc"][^1] > 0); foreach (var values in expected.Values) Assert.All(values, v => Assert.InRange(v, -1, 1)); Check(recovery, 2);
    }
    [Fact]
    public void RawSelectedClosesFeedCenterRangeAndCloseOutput()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 }; var original = selected.Select(_ => Candle(0)).ToArray();
        var expected = BuiltInFormulaReferences.ChartmillOutputs(selected.Select(v => Candle(v)).ToArray(), 2, selected: true);
        foreach (var key in expected.Keys)
        {
            var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.ChartmillValueIndicator, new ChartmillValueIndicatorSpecOptions(2), key), context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
        }
        var batch = Data(original); batch.SetCustomValues(selected.ToList()); batch.CalculateChartmillValueIndicator(length: 2);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var state = new ChartmillValueIndicatorState(length: 2); ((ICustomInputConsumer)state).ReadCloseAsInput();
        for (var i = 0; i < selected.Length; i++)
        {
            var actual = state.Update(Native(Candle(selected[i])), true, true);
            foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
        }
    }
    [Fact]
    public void CustomerAverageReceivesMedianAndDoesNotReplaceAtr()
    {
        var bars = new[] { Candle(1), Candle(2) }; var expected = BuiltInFormulaReferences.ChartmillOutputs(bars, 1, externalCenter: new[] { .5, .5 });
        foreach (var key in expected.Keys) foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((input, period) => { Assert.Equal(1, period); Assert.Equal(new[] { 1d, 1 }, input); return new[] { .5, .5 }; }); using var context = new ComputeContext();
            if (batch) Assert.Equal(expected[key], Data(bars).CalculateChartmillValueIndicator(length: 1).OutputValues[key]);
            else { using var raw = IndicatorCompute.ComputeChartmillValueIndicatorFast(Data(bars), context, 1, key: key); Assert.Equal(expected[key], raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceCenterOrRange()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new ChartmillValueIndicatorState(length: 2); using var control = new ChartmillValueIndicatorState(length: 2);
            var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
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
