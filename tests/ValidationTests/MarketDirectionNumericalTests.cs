using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MarketDirectionNumericalTests
{
    private static Bar[] Candles(params double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("MDI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(MarketDirectionIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCrossings(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
        bars => new Dictionary<string, double[]> { ["Mdi"] = BuiltInFormulaReferences.MarketDirectionOutputs(bars, (IBuiltInIndicator)c.Factory()) }, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesFormula(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory();
        var options = (OoplesFinance.StockIndicators.Builder.Specs.MarketDirectionIndicatorSpecOptions)indicator.CreateOptions();
        var bars = Enumerable.Range(0, Math.Max(64, c.Factory().WarmupBars + 8)).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 20, 40, -5, 30 - i % 7, 1)).ToArray();
        var selected = bars.Select((_, i) => 2d + i % 11).ToArray();
        var projected = bars.Select((bar, i) => new Bar(bar.Time, bar.Open, bar.High, bar.Low, selected[i], bar.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.MarketDirectionOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeMarketDirectionIndicatorFast(data, context, options.Length);
        Assert.Equal(expected, actual.ToArray()); Assert.Equal(selected, data.ChainedValues);
        Assert.Equal(bars.Select(bar => bar.High), data.HighPrices); Assert.Equal(bars.Select(bar => bar.Low), data.LowPrices);
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int fast = 2, int slow = 3)
    {
        var expected = BuiltInFormulaReferences.MarketDirectionValues(bars, fast, slow); var batch = Data(bars).CalculateMarketDirectionIndicator(fast, slow);
        Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Values, batch.OutputValues["Mdi"]); Assert.Equal(expected.Signals, batch.SignalsList);
        var core = new double[bars.Length]; OscillatorCore.MarketDirectionIndicator(bars.Select(b => b.Close).ToArray(), core, fast, slow); Assert.Equal(expected.Values, core);
        if (slow == 55) { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeMarketDirectionIndicatorFast(Data(bars), context, fast); Assert.Equal(expected.Values, output.ToArray()); }
        var native = new MarketDirectionIndicatorState(fast, slow); var direct = new MarketDirectionWindow(fast, slow);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var seed in Candles(9, -2, 7, 4, 6)) { native.Update(Native(seed), true, false); direct.Next(seed.Close, true); }
            native.Reset(); direct.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Candles(-999)[0]), false, false); direct.Next(-999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(Native(bars[i]), final, true); var value = direct.Next(bars[i].Close, final);
                    Assert.Equal(expected.Values[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Mdi"]); Assert.Equal(point.Value, value.Value); Assert.Equal(expected.Signals[i], value.Trade);
                }
            }
        }
        return expected.Values;
    }
    [Fact]
    public void HandCrossingsZeroMidpointsAndEqualPeriods()
    {
        Assert.Equal(new[] { 200d, -200d / 3, -280, 1000d / 3 }, Check(Candles(2, 4, 1, 5)));
        Assert.Equal(new[] { 200d, -200, 0, 0 }, Check(Candles(2, 2, 2, 2)));
        Assert.All(Check(Candles(2, -4, 1, 5), 3, 3), x => Assert.Equal(0, x));
        Assert.Equal(-100, Check(Candles(double.Epsilon), 1, 3)[0]);
        Check(Candles(1, -1, 2, -2, 3, -3, 4));
        Assert.Equal(Check(Candles(2, 4, -1, 3, 7, 1, 2, 6), 2, 5), Check(Candles(2, 4, -1, 3, 7, 1, 2, 6), 5, 2));
    }
    [Fact]
    public void WideAndSubnormalQuotientsRetainUnpublishedCrossings()
    {
        var basis = new[] { 1d, 2, -1, 3, 0, 4, -2, 1 }; var expected = Check(Candles(basis));
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, Math.Pow(2, 1021) }) Assert.Equal(expected, Check(Candles(basis.Select(v => v * scale).ToArray())));
        var e = double.Epsilon; var m = double.MaxValue;
        var recovered = Check(Candles(m, e, 2 * e, 3 * e, 1, -1, 4)); Assert.Equal(double.PositiveInfinity, recovered[2]); Assert.Equal(-40, recovered[3]);
        Check(Candles(m, -m, m / 2, -m / 4, e, -e, 2 * e, 0, 1), 3, 5);
        Check(Candles(m, -Math.BitDecrement(m), m / 2, -m / 2, 1), 1, 55);
        Check(Candles(1, Math.BitIncrement(-1d), 1, Math.BitDecrement(-1d), 1));
    }
    [Fact]
    public void WindowExpiryAndResetKeepIndependentHistories()
    {
        var bars = Candles(8, -3, 4, 4, 1, 9, -2, 7, 0, 3, 3, 3, 1, 2, 5);
        foreach (var pair in new[] { (2, 4), (3, 7), (7, 3), (1, 5), (5, 1), (4, 55) }) Check(bars, pair.Item1, pair.Item2);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var fast in new[] { int.MinValue, 0, 1, 2, 55, int.MaxValue }) foreach (var slow in new[] { int.MinValue, 0, 1, 2, 55, int.MaxValue })
        { Check(Candles(2, 4, -1, 3, 7, 0), fast, slow); Check(Array.Empty<Bar>(), fast, slow); }
    }
    [Fact]
    public void CoreSpanContractsAndInPlaceInput()
    {
        var prices = new[] { 2d, 4, 1, 5 }; var expected = Check(Candles(prices)); var output = Enumerable.Repeat(777d, 6).ToArray();
        OscillatorCore.MarketDirectionIndicator(prices, output, 2, 3); Assert.Equal(expected, output.Take(4)); Assert.Equal(new[] { 777d, 777 }, output.Skip(4));
        var inPlace = prices.ToArray(); OscillatorCore.MarketDirectionIndicator(inPlace, inPlace, 2, 3); Assert.Equal(expected, inPlace);
        Assert.Throws<ArgumentException>(() => OscillatorCore.MarketDirectionIndicator(prices, new double[3], 2, 3));
        OscillatorCore.MarketDirectionIndicator(Array.Empty<double>(), output, 2, 3); Assert.Equal(expected, output.Take(4));
    }
    [Fact]
    public void NonfiniteInputCannotAdvanceStateOrPartiallyWriteCore()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var invalid = Candles(1, bad); Assert.Throws<ArgumentOutOfRangeException>(() => Data(invalid).CalculateMarketDirectionIndicator());
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeMarketDirectionIndicatorFast(Data(invalid), context));
            var output = new[] { 777d, 777 }; Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.MarketDirectionIndicator(new[] { 1d, bad }, output)); Assert.Equal(new[] { 777d, 777 }, output);
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var actual = new MarketDirectionIndicatorState(2, 3); var expected = new MarketDirectionIndicatorState(2, 3); var first = Native(Candles(2)[0]); actual.Update(first, true, true); expected.Update(first, true, true);
                var values = new[] { 4d, 4, 4, 4, 1 }; values[field] = bad; var broken = new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4]);
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(broken), final, true)); var next = Native(Candles(4)[0]); Assert.Equal(expected.Update(next, true, true).Value, actual.Update(next, true, true).Value);
            }
        }
    }
}
