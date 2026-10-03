using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class GroverNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(GroverLlorensActivator) || c.IndicatorType == typeof(GroverLlorensCycleOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAtrTrailAndRsi(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.GroverOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static Bar[] Bars(params double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, Math.Max(v, 0), Math.Min(v, 0), v, 1)).ToArray();
    private static Bar[] Ranged(params double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
    private static StockData Batch(Bar[] bars, bool cycle, MovingAvgType kind, int length, int smooth, double mult)
        => cycle ? Data(bars).CalculateGroverLlorensCycleOscillator(kind, length, smooth, mult) : Data(bars).CalculateGroverLlorensActivator(kind, length, mult);
    private static ComputeBuffer Fast(StockData data, ComputeContext context, bool cycle, MovingAvgType kind, int length, int smooth, double mult)
        => cycle ? IndicatorCompute.ComputeGroverLlorensCycleOscillatorFast(data, context, length, smooth, mult, kind) : IndicatorCompute.ComputeGroverLlorensActivatorFast(data, context, length, mult, kind);
    private static IStreamingIndicatorState State(bool cycle, MovingAvgType kind, int length, int smooth, double mult)
        => cycle ? new GroverLlorensCycleOscillatorState(kind, length, smooth, mult) : new GroverLlorensActivatorState(kind, length, mult);
    private static (double[] Line, Signal[] Signals) Check(Bar[] bars, bool cycle, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod, int reference = 6, int length = 3, int smooth = 2, double mult = 1)
    {
        var expected = BuiltInFormulaReferences.GroverValues(bars, reference, length, smooth, mult, cycle); var key = cycle ? "Glco" : "Gla"; var line = expected.Outputs[key];
        var batch = Batch(bars, cycle, kind, length, smooth, mult); Assert.Equal(line, batch.CustomValuesList); Assert.Equal(line, batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList);
        using (var context = new ComputeContext()) { using var output = Fast(Data(bars), context, cycle, kind, length, smooth, mult); Assert.Equal(line, output.ToArray()); }
        var state = State(cycle, kind, length, smooth, mult); using var lifetime = (IDisposable)state; var window = new GroverWindow(kind, length, smooth, mult, cycle);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Ranged(4, -2, 7, 1)) { state.Update(Native(b), true, false); window.Next(b.Close, b.High, b.Low, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Ranged(999)[0]), false, false); window.Next(-999, 0, -999, false);
                foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, bars[i].High, bars[i].Low, final); Assert.Equal(line[i], point.Value); Assert.Equal(line[i], point.Outputs![key]); Assert.Equal(line[i], direct.Line); Assert.Equal(expected.Signals[i], direct.Trade); }
            }
        }
        return (line, expected.Signals);
    }
    [Fact]
    public void HandAtrTrailRsiAndZeroFallbackAreIndependent()
    {
        var activator = Check(Ranged(2, 4, 8), false, length: 1); Assert.Equal(new[] { 2d, -1, -6 }, activator.Line); Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongBuy }, activator.Signals);
        var cycle = Check(Ranged(2, 4, -2, 3), true, length: 1, smooth: 2); Assert.Equal(new[] { 100d, 100, 250d / 13, 500d / 51 }, cycle.Line); Assert.Equal(new[] { Signal.StrongBuy, Signal.None, Signal.StrongSell, Signal.Sell }, cycle.Signals);
        var collapsed = new[] { 2d, 4, 3 }.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
        Assert.Equal(new[] { 2d, 0, 5 }, Check(collapsed, false, length: 1).Line); Assert.Equal(new[] { 100d, 100, 100 }, Check(collapsed, true, length: 1, smooth: 1).Line);
        Assert.Equal(new[] { 2d, 2, 2 }, Check(Ranged(2, 4, 8), false, length: 1, mult: 0).Line);
        Assert.Equal(new[] { 2d, 5, 10 }, Check(Ranged(2, 4, 8), false, length: 1, mult: -1).Line);
    }
    [Fact]
    public void WideSubnormalTrailsAndNestedMeansMatchExactReference()
    {
        foreach (var cycle in new[] { false, true }) foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Bars(Enumerable.Range(0, 23).Select(i => (i % 7 - 3) * scale).ToArray()), cycle, kind.Kind, kind.Reference, 3, 4, 2);
        var bars = new[] { new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1), new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, -double.MaxValue, 1) }.Concat(Ranged(1, 2, 0, -1, 0, 0, 0, 0)).ToArray();
        foreach (var kind in Kinds) foreach (var mult in new[] { double.MaxValue, double.Epsilon, -2d, 0d, 2d }) foreach (var cycle in new[] { false, true })
        { var result = Check(bars, cycle, kind.Kind, kind.Reference, 2, 3, mult); Assert.All(result.Line, v => Assert.False(double.IsNaN(v))); if (cycle) Assert.All(result.Line, v => Assert.InRange(v, 0, 100)); }
    }
    [Fact]
    public void ExpiryPlateausAndDifferentSmoothingPeriodsKeepSeparateState()
    {
        foreach (var kind in Kinds) foreach (var cycle in new[] { false, true }) foreach (var periods in new[] { (1, 1), (2, 7), (7, 2), (4, 3) })
            Check(Ranged(2, 4, -2, 3, 0, 0, 0, 0, 8, -4, 2, 2, 2, 1), cycle, kind.Kind, kind.Reference, periods.Item1, periods.Item2);
        using var binding = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, n) => new[] { 0d, 0, 0, 0 }, (v, n) => new[] { 0d, 2 * double.Epsilon, 0, 0 } });
        using var context = new ComputeContext(); using var output = Fast(Data(Ranged(1, 2, 3, 4)), context, true, MovingAvgType.WildersSmoothingMethod, 2, 2, 1);
        Assert.Equal(new[] { 100d, 100, 0, 0 }, output.ToArray());
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var cycle in new[] { false, true }) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Ranged(2, -4, 3), cycle, kind.Kind, kind.Reference, period, period); Check(Array.Empty<Bar>(), cycle, kind.Kind, kind.Reference, period, period); }
    }
    [Fact]
    public void CallbackStagesReceiveRangesOscillatorAndSignedMoves()
    {
        var bars = Ranged(2, 4, -2, 3); var atr = new[] { 1d, 2, 3, 4 }; var smooth = new[] { 1d, 2, 0, 1 }; var gains = new[] { 1d, 3, 1, 1 }; var losses = new[] { 1d, 1, 3, 1 };
        foreach (var cycle in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.GroverValues(bars, 6, 1, 2, 1, cycle, atr, cycle ? smooth : null, cycle ? gains : null, cycle ? losses : null); var calls = 0;
            var callbacks = new List<Func<IReadOnlyList<double>, int, IReadOnlyList<double>>> { (v, n) => { Assert.Equal(1, n); Assert.Equal(expected.Ranges, v); calls++; return atr; } };
            if (cycle) { callbacks.Add((v, n) => { Assert.Equal(2, n); Assert.Equal(expected.Oscillators, v); calls++; return smooth; }); callbacks.Add((v, n) => { Assert.Equal(2, n); Assert.Equal(expected.Gains, v); calls++; return gains; }); callbacks.Add((v, n) => { Assert.Equal(2, n); Assert.Equal(expected.Losses, v); calls++; return losses; }); }
            using var binding = ComponentAverage.Arm(callbacks); using var context = new ComputeContext(); using var output = Fast(Data(bars), context, cycle, MovingAvgType.WildersSmoothingMethod, 1, 2, 1);
            Assert.Equal(expected.Outputs[cycle ? "Glco" : "Gla"], output.ToArray()); Assert.Equal(cycle ? 4 : 1, calls); Assert.Equal(calls, ComponentAverage.Requests);
        }
    }
    [Fact]
    public void ActualFlatOverridesAndExhaustedSlotsKeepTheirOwnRatios()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, n) => new[] { 1d, 1, 1 }, (v, n) => new[] { 2d, 2, 2 }, (v, n) => new[] { scale, scale, scale }, (v, n) => new[] { scale, scale, scale } };
            using var binding = ComponentAverage.Arm(callbacks); using var context = new ComputeContext(); using var output = Fast(Data(Ranged(1, 2, 3)), context, true, MovingAvgType.WildersSmoothingMethod, 2, 2, 1); Assert.Equal(new[] { 50d, 50, 50 }, output.ToArray());
        }
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, n) => new[] { 1d, 1, 1 }, (v, n) => new[] { 2d, 2, 2 }, (v, n) => new[] { 1d, 3, 1 }, (v, n) => new[] { 1d, 1, 3 } }))
        { using var context = new ComputeContext(); using var output = Fast(Data(Ranged(1, 2, 3)), context, true, MovingAvgType.WildersSmoothingMethod, 2, 2, 1); Assert.Equal(new[] { 50d, 75, 25 }, output.ToArray()); }
        var bars = Bars(-double.MaxValue, double.MaxValue, 0, -double.MaxValue, 1, 2, 1);
        foreach (var cycle in new[] { false, true })
        { using var binding = ComponentAverage.Arm((v, n) => v); Assert.NotNull(ComponentAverage.Take(new[] { 1d }, 1)); using var context = new ComputeContext(); using var output = Fast(Data(bars), context, cycle, MovingAvgType.WildersSmoothingMethod, 2, 3, double.MaxValue); Assert.Equal(BuiltInFormulaReferences.GroverValues(bars, 6, 2, 3, double.MaxValue, cycle).Outputs[cycle ? "Glco" : "Gla"], output.ToArray()); Assert.Equal(cycle ? 5 : 2, ComponentAverage.Requests); }
    }
    [Fact]
    public void SelectedRangesAndBatchZeroCallbacksRemainConsistent()
    {
        var bars = Ranged(2, 4, -2, 3); var selected = new[] { 20d, 3, -10, 0 }; var projected = bars.Select((b, i) => { var previous = selected[Math.Max(0, i - 1)]; var outside = selected[i] > b.High || selected[i] < b.Low; return new Bar(b.Time, b.Open, outside ? Math.Max(previous, selected[i]) : b.High, outside ? Math.Min(previous, selected[i]) : b.Low, selected[i], b.Volume); }).ToArray();
        foreach (var cycle in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.GroverValues(projected, 6, 2, 3, 1, cycle);
            using (ComponentAverage.Arm((v, n) => throw new InvalidOperationException("Batch consumes no callbacks")))
            { var data = Data(bars); data.SetCustomValues(selected.ToList()); if (cycle) data.CalculateGroverLlorensCycleOscillator(length: 2, smoothLength: 3, mult: 1); else data.CalculateGroverLlorensActivator(length: 2, mult: 1); Assert.Equal(expected.Outputs[cycle ? "Glco" : "Gla"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(0, ComponentAverage.Requests); }
            var fast = Data(bars); fast.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var output = Fast(fast, context, cycle, MovingAvgType.WildersSmoothingMethod, 2, 3, 1); Assert.Equal(expected.Outputs[cycle ? "Glco" : "Gla"], output.ToArray()); Assert.Equal(selected, fast.ChainedValues); Assert.Equal(bars.Select(b => b.Close), fast.ClosePrices);
        }
    }
    [Fact]
    public void InvalidMultiplierAndCandleCannotAdvanceState()
    {
        foreach (var cycle in new[] { false, true })
        {
            foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            { Assert.Throws<ArgumentOutOfRangeException>(() => State(cycle, MovingAvgType.WildersSmoothingMethod, 2, 3, bad)); Assert.Throws<ArgumentOutOfRangeException>(() => Batch(Array.Empty<Bar>(), cycle, MovingAvgType.WildersSmoothingMethod, 2, 3, bad)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => Fast(Data(Array.Empty<Bar>()), context, cycle, MovingAvgType.WildersSmoothingMethod, 2, 3, bad)); }
            foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                var state = State(cycle, MovingAvgType.WildersSmoothingMethod, 2, 3, 1); var control = State(cycle, MovingAvgType.WildersSmoothingMethod, 2, 3, 1); using var a = (IDisposable)state; using var b = (IDisposable)control;
                foreach (var candle in Ranged(2, 4, -1)) { state.Update(Native(candle), true, false); control.Update(Native(candle), true, false); }
                var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
                foreach (var candle in Ranged(3, 0, 2)) Assert.Equal(control.Update(Native(candle), true, true).Value, state.Update(Native(candle), true, true).Value);
            }
        }
    }
    [Fact]
    public void LegacyMeansRetainPublicRouteAgreement()
    {
        var bars = Ranged(4, -2, 7, 0, 3, 1); var kind = MovingAvgType.TripleExponentialMovingAverage;
        foreach (var cycle in new[] { false, true })
        { var batch = Batch(bars, cycle, kind, 3, 2, 1); using var context = new ComputeContext(); using var output = Fast(Data(bars), context, cycle, kind, 3, 2, 1); Assert.Equal(batch.CustomValuesList, output.ToArray()); var state = State(cycle, kind, 3, 2, 1); using var lifetime = (IDisposable)state; for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, false).Value, 11); }
    }
}
