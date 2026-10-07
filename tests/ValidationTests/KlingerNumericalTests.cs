using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KlingerNumericalTests
{
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, MovingAvgType.WildersSmoothingMethod => 6, _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static Bar Candle(double high, double low, double close, double volume = 1) => new(DateTime.UnixEpoch, close, high, low, close, volume);
    private static OhlcvBar Native(Bar b) => new("KVO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(KlingerVolumeOscillator) || c.IndicatorType == typeof(KlingerSignal) || c.IndicatorType == typeof(Kvo)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentTrendSegments(IndicatorValidationCase c, string route)
    {
        var o = ((IBuiltInIndicator)c.Factory()).CreateOptions();
        int Read(string name, int fallback) => o.GetType().GetProperty(name)?.GetValue(o) is int n ? n : fallback;
        var kind = o.GetType().GetProperty("MaType")?.GetValue(o) is MovingAvgType k ? Kind(k) : 3;
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.KlingerValues(bars, Read("FastLength", Read("Length", 34)), Read("SlowLength", 55), Read("SignalLength", 13), kind), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcesPreserveOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static void Check(Bar[] bars, MovingAvgType kind, int fast, int slow, int signal)
    {
        var expected = BuiltInFormulaReferences.KlingerValues(bars, fast, slow, signal, Kind(kind));
        var batch = Data(bars).CalculateKlingerVolumeOscillator(kind, fast, slow, signal);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var series in new[] { IndicatorCompute.KlingerSeries.Oscillator, IndicatorCompute.KlingerSeries.Signal, IndicatorCompute.KlingerSeries.Histogram })
        {
            using var result = IndicatorCompute.ComputeKlingerVolumeOscillatorFast(Data(bars), context, fast, slow, signal, kind, series);
            Assert.Equal(expected[series == IndicatorCompute.KlingerSeries.Oscillator ? "Kvo" : series == IndicatorCompute.KlingerSeries.Signal ? "KvoSignal" : "KvoHistogram"], result.ToArray());
        }
        using var state = new KlingerVolumeOscillatorState(kind, fast, slow, signal);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(9, -3, 5, 13)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue, -double.MaxValue, -1, double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected["Kvo"][i], result.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], result.Outputs![key]);
                }
            }
        }
    }

    [Fact]
    public void SubnormalTrendHasAnIndependentRationalHand()
    {
        var bars = new[] { Candle(1, 0, 0), Candle(1, 0, double.Epsilon), Candle(1, 0, 2 * double.Epsilon) };
        // Directions0,1,1; cumulative ranges1,2,3; force0,0,100/3.
        // EMA1 minus EMA2 on bar3 = 100/3 - 200/9 = 100/9.
        Assert.Equal(100d / 9, BuiltInFormulaReferences.KlingerValues(bars, 1, 2, 1, 3)["Kvo"][2]);
        Check(bars, MovingAvgType.ExponentialMovingAverage, 1, 2, 1);
        var early = new[] { Candle(1, 0, 0), Candle(3, 0, 1), Candle(2, 0, 0), Candle(5, 0, 2), Candle(4, 0, 1) };
        // The first rising bar has range3/cumulative4, hence force50. EMA1-EMA2 = 50-25.
        Assert.Equal(25, BuiltInFormulaReferences.KlingerValues(early, 1, 2, 2, 3)["Kvo"][1]);
        Check(early, MovingAvgType.ExponentialMovingAverage, 1, 2, 2);
        Assert.Equal(new[] { Signal.None, Signal.None, Signal.StrongBuy }, Data(bars).CalculateKlingerVolumeOscillator(fastLength: 1, slowLength: 2, signalLength: 2).SignalsList);
    }

    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void ExactForcesSurviveOverflowTiesReversalsAndZeroRange(MovingAvgType kind)
    {
        var m = double.MaxValue;
        var bars = new[] { Candle(m, -m, 0, m), Candle(m, -m, 1, m), Candle(m, -m, 2, m), Candle(m, -m, 2, -m), Candle(m, -m, -1, m), Candle(0, 0, 0, 0), Candle(3, 1, 2, 5) };
        Check(bars, kind, 2, 3, 2);
        Check(bars, kind, int.MaxValue, int.MaxValue - 1, int.MaxValue);
        Check(bars, kind, 0, -1, 0);
        var finite = new[] { Candle(1, 0, 0), Candle(3, 0, 1), Candle(2, 0, 0), Candle(5, 0, 2), Candle(4, 0, 1), Candle(4, 0, 1), Candle(7, 0, 3) };
        Check(finite, kind, 2, 3, 2);
        Assert.Equal(25, BuiltInFormulaReferences.KlingerValues(finite, 2, 3, 2, 1)["Kvo"][1]);
    }

    [Fact]
    public void CoreSpansAllowOverlapAndKeepSuffixes()
    {
        var bars = Enumerable.Range(0, 19).Select(i => Candle(7 + i % 3, -3 - i % 2, i % 7 - 2, 3 + i % 5)).ToArray();
        var expected = BuiltInFormulaReferences.KlingerValues(bars, 2, 5, 3, 3);
        foreach (var signal in new[] { false, true })
        foreach (var alias in Enumerable.Range(0, 4))
        {
            var inputs = new[] { bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray() };
            var target = inputs[alias].Concat(new[] { 123d, 456d }).ToArray(); inputs[alias] = target;
            if (signal) OscillatorCore.KlingerSignal(inputs[0].AsSpan(0, bars.Length), inputs[1].AsSpan(0, bars.Length), inputs[2].AsSpan(0, bars.Length), inputs[3].AsSpan(0, bars.Length), target.AsSpan(1), 2, 5, 3);
            else VolumeCore.KlingerVolumeOscillator(inputs[0].AsSpan(0, bars.Length), inputs[1].AsSpan(0, bars.Length), inputs[2].AsSpan(0, bars.Length), inputs[3].AsSpan(0, bars.Length), target.AsSpan(1), 2, 5);
            Assert.Equal(expected[signal ? "KvoSignal" : "Kvo"], target.Skip(1).Take(bars.Length));
            Assert.Equal(456, target[^1]);
        }
        Assert.Throws<ArgumentException>(() => VolumeCore.KlingerVolumeOscillator(new double[1], [], new double[1], new double[1], new double[1]));
        Assert.Throws<ArgumentException>(() => OscillatorCore.KlingerSignal(new double[1], new double[1], new double[1], new double[1], []));
    }

    [Fact]
    public void DirectChainedPricesPreserveOriginalCandles()
    {
        var bars = new[] { Candle(7, -2, 3, 4), Candle(8, -1, 2, 5), Candle(5, -3, 1, 6) };
        var prices = new[] { 1d, -1, 10 }; var data = Data(bars); data.SetCustomValues(prices.ToList());
        var expected = BuiltInFormulaReferences.KlingerValues(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray(), 1, 2, 2, 3);
        // Selected sums6,6,12 give positive force600/17; original sums8,9,3 give negative force.
        Assert.Equal(200d / 17, expected["Kvo"][2]);
        using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeKlingerVolumeOscillatorFast(data, context, 1, 2, 2);
        Assert.Equal(expected["Kvo"], fast.ToArray());
        Assert.Equal(expected["Kvo"], data.CalculateKlingerVolumeOscillator(fastLength: 1, slowLength: 2, signalLength: 2).OutputValues["Kvo"]);
    }

    [Fact]
    public void EqualLegsCancelUnpublishableForcesExactly()
    {
        var bars = new[] { Candle(1, 0, 0, double.MaxValue), Candle(1, 0, .25, double.MaxValue), Candle(1, 0, .5, double.MaxValue) };
        var result = Data(bars).CalculateKlingerVolumeOscillator(fastLength: 1, slowLength: 1, signalLength: 1);
        foreach (var values in result.OutputValues.Values) Assert.Equal(new double[3], values);
        Assert.Equal(new[] { Signal.None, Signal.None, Signal.None }, result.SignalsList);
        Check(bars, MovingAvgType.ExponentialMovingAverage, 1, 1, 1);
    }

    [Fact]
    public void CallbacksKeepOrderPeriodsShortOutputsAndInputRestoration()
    {
        var bars = new[] { Candle(1, 0, 0), Candle(1, 0, .25), Candle(1, 0, .5) };
        foreach (var ema in new[] { false, true })
        {
            var callbacks = new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>>();
            if (!ema)
            {
                callbacks.Add((values, length) => { Assert.Equal(1, length); Assert.Equal(new[] { 0d, 0, 100d / 3 }, values); return new[] { 4d, 3 }; });
                callbacks.Add((values, length) => { Assert.Equal(2, length); Assert.Equal(new[] { 0d, 0, 100d / 3 }, values); return new[] { 1d }; });
            }
            callbacks.Add((values, length) => { Assert.Equal(3, length); Assert.Equal(ema ? new[] { 0d, 0, 100d / 9 } : new[] { 3d, 3, 0 }, values); return new[] { 7d }; });
            using var armed = ComponentAverage.Arm(callbacks);
            using var context = new ComputeContext(); var data = Data(bars);
            using var output = IndicatorCompute.ComputeKlingerVolumeOscillatorFast(data, context, 1, 2, 3,
                ema ? MovingAvgType.ExponentialMovingAverage : MovingAvgType.WeightedMovingAverage, IndicatorCompute.KlingerSeries.Signal);
            Assert.Equal(new[] { 7d, 0, 0 }, output.ToArray()); Assert.Equal(ema ? 1 : 3, ComponentAverage.Requests);
            Assert.Equal(bars.Select(b => b.Close), data.InputValues);
        }
    }

    [Fact]
    public void NonfiniteInputsDoNotAdvanceStateOrReachCallbacks()
    {
        foreach (var slot in Enumerable.Range(0, 4))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var fields = new[] { 4d, 1, 3, 5 }; fields[slot] = invalid;
            var bad = Candle(fields[0], fields[1], fields[2], fields[3]);
            using var state = new KlingerVolumeOscillatorState(); using var control = new KlingerVolumeOscillatorState();
            var seed = Native(Candle(3, 1, 2)); state.Update(seed, true, false); control.Update(seed, true, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(bad), true, true));
            var next = Native(Candle(5, 2, 4)); Assert.Equal(control.Update(next, true, true).Value, state.Update(next, true, true).Value);
            using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Invalid input reached a callback."));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(new[] { bad }).CalculateKlingerVolumeOscillator());
            Assert.Equal(0, ComponentAverage.Requests);
        }
    }
}
