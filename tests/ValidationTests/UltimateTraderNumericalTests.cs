using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimateTraderNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static Bar Candle(double open, double high, double low, double close, double volume = 1) => new(DateTime.UnixEpoch, open, high, low, close, volume);
    private static OhlcvBar Native(Bar b) => new("UTO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Mixed() => Enumerable.Range(0, 32).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i % 7 - 3, 8 + i % 5, -5 - i % 3, i * 7 % 13 - 6, i * 11 % 17)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(UltimateTraderOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentSixScores(IndicatorValidationCase c, string route)
    {
        var options = (UltimateTraderOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.UltimateTraderValues(bars, options.MaType), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesKeepOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static void Check(Bar[] bars, MovingAvgType kind, int lb, int smooth, int range)
    {
        var expected = BuiltInFormulaReferences.UltimateTraderValues(bars, kind, lb, smooth, range);
        var batch = Data(bars).CalculateUltimateTraderOscillator(kind, 17, lb, smooth, range);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeUltimateTraderOscillatorFast(Data(bars), context, 17, kind, lb, smooth, range);
        Assert.Equal(expected["Uto"], fast.ToArray());
        var core = new double[bars.Length];
        OscillatorCore.UltimateTraderOscillator(bars.Select(b => b.Open).ToArray(), bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), core, lb, smooth, range, kind);
        Assert.Equal(expected["Uto"], core);
        using var state = new UltimateTraderOscillatorState(kind, 17, lb, smooth, range);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(-3, 9, -9, 6, 11)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(0, double.MaxValue, -double.MaxValue, -double.MaxValue, double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected["Uto"][i], actual.Value);
                    Assert.Equal(expected["Signal"][i], actual.Outputs!["Signal"]);
                }
            }
        }
        var trades = new Signal[bars.Length]; var previous = ReferenceFraction.FromDouble(0);
        for (var i = 0; i < trades.Length; i++)
        {
            var difference = ReferenceFraction.FromDouble(expected["Uto"][i]) - ReferenceFraction.FromDouble(expected["Signal"][i]);
            trades[i] = difference.Sign > 0 && difference.CompareTo(previous) > 0 ? Signal.StrongBuy
                : difference.Sign < 0 && difference.CompareTo(previous) < 0 ? Signal.StrongSell
                : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
            previous = difference;
        }
        Assert.Equal(trades, batch.SignalsList);
    }

    [Fact]
    public void IndependentHandsIncludeMixedScoresFlatChangeAndZeroMagnitude()
    {
        var bars = new[] { Candle(1, 4, 0, 3, 2), Candle(3, 8, 1, 2, 5), Candle(2, 3, 0, 1, 1), Candle(1, 5, -3, 4, 4), Candle(4, 4, 4, 4, 4), Candle(4, 6, 2, 3, 0) };
        // Last scores: -25,-50,100/3,-100/9,-100,0. Their signed
        // sum is -1375/9, magnitude 1975/9, hence -5500/79.
        var hand = new[] { 100d, -100, -100, 100, 0, -5500d / 79 };
        Assert.Equal(hand, BuiltInFormulaReferences.UltimateTraderRaw(bars, 2, 3));
        Assert.Equal(hand, UltimateTraderWindow.Raw(Data(bars), 2, 3));
        Check(bars, MovingAvgType.WeightedMovingAverage, 2, 3, 3);
        Check(new[] { Candle(0, 0, 0, 0, 0), Candle(0, 0, 0, 0, 0) }, MovingAvgType.SimpleMovingAverage, 1, 1, 1);
    }

    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void PeriodsSmoothingPreviewResetAndSignals(MovingAvgType kind)
    {
        foreach (var periods in new[] { (1, 1, 1), (2, 3, 4), (5, 4, 2), (7, 2, 3) })
            Check(Mixed(), kind, periods.Item1, periods.Item2, periods.Item3);
        // Opposing first-bar body/position and momentum distinguish zero-price
        // momentum startup from the own-close true-range startup.
        Check(new[] { Candle(3, 4, 0, 1) }, kind, 1, 1, 1);
        Check(new[] { Candle(0, 2, -2, 10, 5), Candle(0, 2, -1, 1, 8), Candle(3, 4, 0, 2, 2),
            Candle(0, 4, -1, -8, 7), Candle(-2, 0, -3, -1, 1) }, kind, 2, 1, 3);
        Check(Array.Empty<Bar>(), kind, 5, 4, 2);
        Check(Mixed().Take(5).ToArray(), kind, int.MaxValue, int.MaxValue, int.MaxValue);
        Check(Mixed().Take(5).ToArray(), kind, 0, -1, 0);
    }

    [Fact]
    public void ExtremeAndSubnormalCandlesKeepExactNormalizedScores()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var endpoint = new[] { Candle(-scale, scale, -scale, scale, scale) };
            Assert.Equal(new[] { 100d }, UltimateTraderWindow.Raw(Data(endpoint), 1, 1));
            Check(endpoint, MovingAvgType.WeightedMovingAverage, 1, 1, 1);
            var bars = Enumerable.Range(0, 18).Select(i => Candle((i % 3 - 1) * scale, scale, -scale, ((i + 1) % 3 - 1) * scale, i % 2 == 0 ? 0 : scale)).ToArray();
            Check(bars, MovingAvgType.WeightedMovingAverage, 3, 2, 4);
        }
        // Selected prices can be outside the original candle range. Individual
        // scores can then be larger than double.MaxValue, while the ratio is finite.
        var extremes = new[] { Candle(0, double.Epsilon, 0, double.MaxValue, 0), Candle(double.Epsilon, double.Epsilon, 0, -double.MaxValue, double.MaxValue), Candle(0, double.Epsilon, 0, double.Epsilon, 1), Candle(0, double.MaxValue, -double.MaxValue, 0, double.Epsilon) };
        Check(extremes, MovingAvgType.WeightedMovingAverage, 2, 1, 2);
        var raw = UltimateTraderWindow.Raw(Data(extremes), 2, 2);
        Assert.All(raw, value => Assert.InRange(value, -100, 100));
    }

    [Fact]
    public void CoreChecksSpansAndReadsBeforeWritingAliases()
    {
        var bars = Mixed(); var o = bars.Select(b => b.Open).ToArray(); var h = bars.Select(b => b.High).ToArray(); var l = bars.Select(b => b.Low).ToArray(); var c = bars.Select(b => b.Close).ToArray(); var v = bars.Select(b => b.Volume).ToArray();
        var expected = BuiltInFormulaReferences.UltimateTraderValues(bars, MovingAvgType.WeightedMovingAverage)["Uto"];
        foreach (var slot in Enumerable.Range(0, 5))
        {
            var arrays = new[] { o, h, l, c, v }.Select(x => x.ToArray()).ToArray();
            var aliased = new double[bars.Length + 2]; arrays[slot].CopyTo(aliased, 0); arrays[slot] = aliased;
            OscillatorCore.UltimateTraderOscillator(arrays[0].AsSpan(0, bars.Length), arrays[1].AsSpan(0, bars.Length), arrays[2].AsSpan(0, bars.Length), arrays[3].AsSpan(0, bars.Length), arrays[4].AsSpan(0, bars.Length), aliased.AsSpan(1, bars.Length));
            Assert.Equal(expected, aliased.Skip(1).Take(bars.Length)); Assert.Equal(0, aliased[^1]);
        }
        Assert.Throws<ArgumentException>(() => OscillatorCore.UltimateTraderOscillator(o, h, l, c, v, new double[c.Length - 1]));
        foreach (var slot in Enumerable.Range(0, 5))
        {
            var arrays = new[] { o, h, l, c, v }.Select(x => x.ToArray()).ToArray(); arrays[slot] = new double[c.Length - 1];
            Assert.Throws<ArgumentException>(() => OscillatorCore.UltimateTraderOscillator(arrays[0], arrays[1], arrays[2], arrays[3], arrays[4], new double[c.Length]));
        }
    }

    [Fact]
    public void InvalidFieldsCannotAdvanceStateOrInvokeCallbacks()
    {
        foreach (var slot in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new UltimateTraderOscillatorState(); using var control = new UltimateTraderOscillatorState();
            var seed = Native(Candle(1, 3, 0, 2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var values = new[] { 1d, 3, 0, 2, 1 }; values[slot] = invalid;
            var bad = Candle(values[0], values[1], values[2], values[3], values[4]);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(bad), final, true));
            foreach (var bar in Mixed().Take(8)) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { bad }).CalculateUltimateTraderOscillator());
            using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Invalid input reached a callback."));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeUltimateTraderOscillatorFast(Data(new[] { bad }), context));
            Assert.Equal(0, ComponentAverage.Requests);
        }
    }

    [Fact]
    public void FastCallbacksPreservePeriodsAndThreeStageOrder()
    {
        var bars = Mixed(); var raw = BuiltInFormulaReferences.UltimateTraderRaw(bars, 5, 2);
        var first = new[] { 7d, -2, 4 }; var second = new[] { -3d, 8 }; var third = new[] { 11d };
        double[] Pad(double[] values) => values.Concat(new double[bars.Length - values.Length]).ToArray();
        foreach (var key in new[] { "Uto", "Signal" })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, length) => { Assert.Equal(5, length); Assert.Equal(raw, input); return first; },
                (input, length) => { Assert.Equal(4, length); Assert.Equal(Pad(first), input); return second; },
                (input, length) => { Assert.Equal(4, length); Assert.Equal(Pad(second), input); return third; }
            });
            using var context = new ComputeContext();
            using var actual = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.UltimateTraderOscillator, new UltimateTraderOscillatorSpecOptions(10), key), context);
            Assert.NotNull(actual); Assert.Equal(Pad(key == "Uto" ? second : third), actual.Value.ToArray());
            Assert.Equal(key == "Uto" ? 2 : 3, ComponentAverage.Requests);
        }
    }

    [Fact]
    public void DirectChainedPricesPreserveOriginalCandles()
    {
        var bars = Mixed();
        var selected = bars.Select((_, i) => (double)(i * 13 % 29 - 14)).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.UltimateTraderValues(projected, MovingAvgType.WeightedMovingAverage);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        Assert.Equal(BuiltInFormulaReferences.UltimateTraderRaw(projected, 5, 2), UltimateTraderWindow.Raw(data, 5, 2));
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeUltimateTraderOscillatorFast(data, context);
        Assert.Equal(expected["Uto"], fast.ToArray());
        Assert.Equal(selected, data.ChainedValues);
        data.CalculateUltimateTraderOscillator();
        foreach (var key in expected.Keys) Assert.Equal(expected[key], data.OutputValues[key]);
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        Assert.Equal(bars.Select(b => b.Open), data.OpenPrices);
        Assert.Equal(bars.Select(b => b.High), data.HighPrices);
        Assert.Equal(bars.Select(b => b.Low), data.LowPrices);
        Assert.Equal(bars.Select(b => b.Volume), data.Volumes);
    }

    [Fact]
    public void UnsupportedAverageDoesNotAcquireAnInventedReference()
    {
        Assert.Empty(BuiltInFormulaReferences.For(new UltimateTraderOscillator(10, new Hma(5))));
        Assert.NotEmpty(BuiltInFormulaReferences.For(new UltimateTraderOscillator(10, new Sma(5))));
    }
}
