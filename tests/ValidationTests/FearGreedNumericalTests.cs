using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FearGreedNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FearAndGreedIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDirectionalStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FearGreedOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int fast = 2, int slow = 4, int smooth = 3, MovingAvgType kind = MovingAvgType.WeightedMovingAverage, int reference = 2)
    {
        var expected = BuiltInFormulaReferences.FearGreedValues(bars, fast, slow, smooth, reference); var batch = Data(bars).CalculateFearAndGreedIndicator(kind, fast, slow, smooth);
        Assert.Equal(expected.Outputs["Fgi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Fgi", "Signal" })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFearAndGreedFast(Data(bars), context, fast, slow, kind, smooth, key == "Signal"); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new FearAndGreedIndicatorState(kind, fast, slow, smooth); using var window = new FearGreedWindow(kind, fast, slow, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (3, -1, 2), (9, 3, 6), (7, -3, 1))) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 999))[0]), false, false); window.Next(999, -999, 999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    Assert.Equal(expected.Outputs["Fgi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Fgi"]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade);
                    Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]); Assert.Equal(point.Outputs["Signal"], direct.SignalLine);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandDirectionGapsTiesAndSignalLevels()
    {
        var bars = Candles((2, 0, 1), (4, 2, 3), (8, 6, 7), (5, 3, 4), (9, 1, 4));
        var result = Check(bars, 1, 2, 2, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(new[] { 0d, 1.5, 1, -4.5, 2 }, result.Outputs["Fgi"]);
        Assert.Equal(new[] { 0d, .75, 1.25, -1.75, -1.25 }, result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongBuy, Signal.StrongSell, Signal.Sell }, result.Signals);
        var inverted = Check(Candles(bars.Select(b => (-b.Low, -b.High, -b.Close)).ToArray()), 1, 2, 2, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(result.Outputs["Fgi"].Select(v => -v), inverted.Outputs["Fgi"]);
        var swapped = Check(bars, 2, 1, 2, MovingAvgType.SimpleMovingAverage, 1); Assert.Equal(result.Outputs["Fgi"].Select(v => -v), swapped.Outputs["Fgi"]);
    }
    [Fact]
    public void WideRangesPreserveSeparateRoundedStagesAndRecover()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Candles(Enumerable.Range(0, 21).Select(i => (7 * scale, -7 * scale, (i % 9 - 4) * scale)).ToArray()), kind: kind.Kind, reference: kind.Reference);
        var bars = Candles((double.MaxValue, -double.MaxValue, -double.MaxValue), (double.MaxValue, -double.MaxValue, double.MaxValue), (double.MaxValue, -double.MaxValue, 0), (4, -2, 1), (4, -2, 2), (4, -2, -1), (3, -1, 1));
        foreach (var kind in Kinds) Check(bars, kind: kind.Kind, reference: kind.Reference);
    }
    [Fact]
    public void EqualPeriodsCancelUnrepresentableRanges()
    {
        var m = double.MaxValue; var bars = Candles((m, -m, -m), (m, -m, m), (m, -m, -m), (m, -m, m));
        foreach (var kind in Kinds) foreach (var length in new[] { 1, 2, 3 })
        { var result = Check(bars, length, length, 2, kind.Kind, kind.Reference); Assert.All(result.Outputs["Fgi"], v => Assert.Equal(0, v)); Assert.All(result.Outputs["Signal"], v => Assert.Equal(0, v)); }
    }
    [Fact]
    public void ExtendedLineKeepsFiniteSignalAndExhaustedOverrides()
    {
        var m = double.MaxValue; var bars = Candles((m, -m, -m), (m, -m, m));
        var expected = Check(bars, 1, 3, 2, MovingAvgType.SimpleMovingAverage, 1);
        Assert.True(double.IsPositiveInfinity(expected.Outputs["Fgi"][1])); Assert.True(double.IsFinite(expected.Outputs["Signal"][1]));
        var calls = 0; using var armed = ComponentAverage.Arm((v, _) => { calls++; return v; }); Assert.NotNull(ComponentAverage.Take(new[] { 1d }, 1));
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFearAndGreedFast(Data(bars), context, 1, 3, MovingAvgType.SimpleMovingAverage, 2, true);
        Assert.Equal(expected.Outputs["Signal"], output.ToArray()); Assert.Equal(1, calls);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var slot in Enumerable.Range(0, 3))
        { var periods = new[] { 2, 4, 3 }; periods[slot] = length; Check(Candles((4, -2, 1), (4, -2, -1), (7, 0, 3), (1, -1, 0)), periods[0], periods[1], periods[2], kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), periods[0], periods[1], periods[2], kind.Kind, kind.Reference); }
    }
    [Fact]
    public void ComponentSlotsPreservePeriodsInputsAndSelectedPrices()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var prices = new[] { 5d, 12, 6, 15 };
        var selectedBars = bars.Select((b, i) => new Bar(b.Time, b.Open, prices[i] >= b.Low && prices[i] <= b.High ? b.High : Math.Max(i == 0 ? prices[i] : prices[i - 1], prices[i]), prices[i] >= b.Low && prices[i] <= b.High ? b.Low : Math.Min(i == 0 ? prices[i] : prices[i - 1], prices[i]), prices[i], b.Volume)).ToArray();
        var up = new[] { 0d, 7, 0, 9 }; var down = new[] { 0d, 0, 11, 0 };
        var selectedExpected = BuiltInFormulaReferences.FearGreedValues(selectedBars, 2, 4, 3, 2);
        foreach (var key in new[] { "Fgi", "Signal" })
        { var selected = Data(bars); selected.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFearAndGreedFast(selected, context, 2, 4, smoothLength: 3, signal: key == "Signal"); Assert.Equal(selectedExpected.Outputs[key], output.ToArray()); }
        foreach (var route in new[] { "batch", "Fgi", "Signal" })
        {
            var calls = 0; var data = Data(bars); data.SetCustomValues(prices.ToList());
            var values = new[] { new[] { 2d, 4, 6, 8 }, new[] { 1d, 2, 3, 4 }, new[] { 0d, 1, 2, 3 }, new[] { 0d, 0, 0, 0 }, new[] { 7d, 8, 9, 10 } };
            var inputs = new[] { up, down, up, down, new[] { 1d, 1, 1, 1 } }; var periods = new[] { 2, 2, 4, 4, 3 };
            using var armed = ComponentAverage.Arm(Enumerable.Range(0, 5).Select(slot => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((v, period) => { calls++; Assert.Equal(periods[slot], period); Assert.Equal(inputs[slot], v); return values[slot]; })).ToArray());
            if (route == "batch") { data.CalculateFearAndGreedIndicator(fastLength: 2, slowLength: 4, smoothLength: 3); var expected = BuiltInFormulaReferences.FearGreedValues(selectedBars, 2, 4, 3, 2); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); }
            else { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFearAndGreedFast(data, context, 2, 4, smoothLength: 3, signal: route == "Signal"); Assert.Equal(route == "Signal" ? values[4] : inputs[4], output.ToArray()); Assert.Equal(prices, data.ChainedValues); }
            Assert.Equal(route == "batch" ? 0 : route == "Signal" ? 5 : 4, calls);
        }
    }
    [Fact]
    public void LegacyKindsPreserveAllRoutes()
    {
        var bars = Candles((4, 1, 2), (6, 0, 3), (5, 2, 2), (8, 1, 7), (2, -3, -1), (4, 0, 3));
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateFearAndGreedIndicator(kind, 2, 4, 3); using var state = new FearAndGreedIndicatorState(kind, 2, 4, 3); using var context = new ComputeContext();
            foreach (var key in new[] { "Fgi", "Signal" }) { using var output = IndicatorCompute.ComputeFearAndGreedFast(Data(bars), context, 2, 4, kind, 3, key == "Signal"); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
            for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in batch.OutputValues.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); }
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FearAndGreedIndicatorState(fastLength: 2, slowLength: 4, smoothLength: 3); using var control = new FearAndGreedIndicatorState(fastLength: 2, slowLength: 4, smoothLength: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
