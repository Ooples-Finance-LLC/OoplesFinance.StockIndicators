using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class LbrPaintNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("LBR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(LBRPaintBars)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentBands(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.LbrPaintOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, int lookback = 4, double factor = 2.5, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1)
    {
        var expected = BuiltInFormulaReferences.LbrPaintValues(bars, length, lookback, factor, reference); var batch = Data(bars).CalculateLBRPaintBars(kind, length, lookback, factor);
        Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in expected.Outputs.Keys)
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeLBRPaintBarsFast(Data(bars), context, length, factor, kind, lookback, key); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var native = new LBRPaintBarsState(kind, length, lookback, factor); using var window = new LbrPaintWindow(kind, length, lookback, factor);
        for (var pass = 0; pass < 2; pass++)
        {
            native.Update(Native(Candles((999, -999, 888))[0]), true, false); window.Next(999, -999, 888, true);
            native.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Candles((100, -100, 99))[0]), false, false); window.Next(100, -100, 99, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = native.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]);
                    Assert.Equal(expected.Outputs["Aatr"][i], point.Value); Assert.Equal(point.Value, direct.Width); Assert.Equal(expected.Signals[i], direct.Trade);
                    Assert.Equal(expected.Outputs["UpperBand"][i], direct.Upper); Assert.Equal(expected.Outputs["LowerBand"][i], direct.Lower);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandCrossedBandsRetainExtremaAndMeanStartup()
    {
        var first = Check(Candles((3, 1, 2)), 1, 1); Assert.Equal(-2, first.Outputs["UpperBand"][0]); Assert.Equal(6, first.Outputs["LowerBand"][0]); Assert.Equal(5, first.Outputs["Aatr"][0]);
        var retained = Check(Candles((8, 0, 4), (12, -4, 6), (5, 1, 3), (6, 2, 4), (7, 3, 5)), 1, 3, .25);
        Assert.Equal(new[] { 6d, 8, 10.75, 11, 6 }, retained.Outputs["UpperBand"]); Assert.Equal(new[] { 2d, 0, -2.75, -3, 2 }, retained.Outputs["LowerBand"]);
        var startup = Check(Candles((10, 0, 9), (10, 0, 9), (10, 0, 9), (10, 0, 9)), 3, 3, .25);
        Assert.Equal(new[] { 0d, 0, 2.5, 2.5 }, startup.Outputs["Aatr"]); Assert.Equal(new[] { Signal.None, Signal.None, Signal.StrongBuy, Signal.Buy }, startup.Signals);
        foreach (var kind in Kinds) Check(Candles((4, -1, 2), (8, 0, 7), (3, -4, -2), (9, 3, 5), (6, -1, 0), (2, -3, 1)), 3, 3, .4, kind.Kind, kind.Reference);
    }
    [Fact]
    public void WidthCanOverflowWhileBothBandsRemainFinite()
    {
        var m = double.MaxValue; var result = Check(Candles((m, -m, 0)), 1, 1, 1);
        Assert.Equal(double.PositiveInfinity, result.Outputs["Aatr"][0]); Assert.Equal(-m, result.Outputs["UpperBand"][0]); Assert.Equal(m, result.Outputs["LowerBand"][0]);
        var middle = Check(Candles((m, -m, 0)), 1, 1, .5); Assert.Equal(0, middle.Outputs["UpperBand"][0]); Assert.Equal(0, middle.Outputs["LowerBand"][0]);
        var tiny = Check(Candles((double.Epsilon, 0, 0)), 1, 1, .5); Assert.Equal(0, tiny.Outputs["Aatr"][0]); Assert.Equal(0, tiny.Outputs["UpperBand"][0]); Assert.Equal(0, tiny.Outputs["LowerBand"][0]);
    }
    [Fact]
    public void WideRangeSmoothingAndSignedMultipliers()
    {
        var m = double.MaxValue;
        foreach (var kind in Kinds) foreach (var factor in new[] { -m, -.5, 0, double.Epsilon, .25, .5, 1, 2.5, m })
            Check(Candles((m, -m, 0), (m, 0, m), (1, -1, 0), (2, -1, 1), (0, -2, -1), (3, -1, 2)), 2, 3, factor, kind.Kind, kind.Reference);
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, m / 16 })
            Check(Candles(Enumerable.Range(0, 19).Select(i => ((i % 5 + 1) * scale, -(i % 7 + 1) * scale, (i % 3 - 1) * scale)).ToArray()), 3, 4, .25, kind.Kind, kind.Reference);
    }
    [Fact]
    public void SignalMarginsDistinguishAdjacentPrices()
    {
        var values = new[] { .5, Math.BitIncrement(.5), Math.BitIncrement(.5), Math.BitDecrement(.5), Math.BitDecrement(.5) };
        var result = Check(Candles(values.Select(v => (1d, 0d, v)).ToArray()), 1, 1, .5);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.StrongSell, Signal.Sell }, result.Signals);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { var bars = Candles((4, 0, 2), (8, 2, 7), (5, -1, 3), (3, -2, 0)); Check(bars, period, 3, .25, kind.Kind, kind.Reference); Check(bars, 3, period, .25, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), period, period, 0, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void CustomAtrSlotKeepsTrueRangeAndSelectedPrices()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var prices = new[] { 5d, 12, 6, 15 };
        foreach (var key in new[] { "UpperBand", "LowerBand", "Aatr" })
        {
            var data = Data(bars); data.SetCustomValues(prices.ToList()); var calls = 0;
            using var armed = ComponentAverage.Arm((v, length) => { calls++; Assert.Equal(3, length); Assert.Equal(new[] { 8d, 7, 11, 9 }, v); return new[] { 1d, 2, 3, 4 }; });
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeLBRPaintBarsFast(data, context, 3, .5, lbLength: 2, outputKey: key);
            var expected = key == "UpperBand" ? new[] { 8.5, 11, 10.5, 13 } : key == "LowerBand" ? new[] { 1.5, 2, 2.5, 3 } : new[] { .5, 1, 1.5, 2 };
            Assert.Equal(expected, output.ToArray()); Assert.Equal(prices, data.ChainedValues); Assert.Equal(1, calls);
        }
    }
    [Fact]
    public void LegacyKindsRetainAllRoutes()
    {
        var bars = Candles((4, -1, 2), (8, 0, 7), (3, -4, -2), (9, 3, 5), (6, -1, 0), (2, -3, 1));
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateLBRPaintBars(kind, 2, 3, .5); using var native = new LBRPaintBarsState(kind, 2, 3, .5);
            foreach (var key in batch.OutputValues.Keys) { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeLBRPaintBarsFast(Data(bars), context, 2, .5, kind, 3, key); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
            for (var i = 0; i < bars.Length; i++) { var point = native.Update(Native(bars[i]), true, true); foreach (var key in batch.OutputValues.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); }
        }
    }
    [Fact]
    public void InvalidMultiplierAndBarsCannotAdvanceState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LBRPaintBarsState(atrMult: bad)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateLBRPaintBars(atrMult: bad));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeLBRPaintBarsFast(Data(Array.Empty<Bar>()), context, atrMult: bad));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new LBRPaintBarsState(length: 2, lbLength: 3); using var control = new LBRPaintBarsState(length: 2, lbLength: 3);
                foreach (var b in Candles((4, 0, 2), (8, 2, 7))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var values = new[] { 2d, 3, 1, 2, 1 }; values[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
                foreach (var b in Candles((3, 0, 1), (8, -2, 7), (0, -1, 0))) { var a = state.Update(Native(b), true, true); var e = control.Update(Native(b), true, true); foreach (var key in e.Outputs!.Keys) Assert.Equal(e.Outputs[key], a.Outputs![key]); }
            }
        }
    }
}
