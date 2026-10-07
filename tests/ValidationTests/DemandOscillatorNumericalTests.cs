using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DemandOscillatorNumericalTests
{
    private static Bar B(double h, double l, double c, double v = 1) => new(DateTime.UnixEpoch, c, h, l, c, v);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Ref)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(DemandOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentPressureStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.DemandOscillatorOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int reference = 3, int average = 3, int range = 2, int line = 4)
    {
        var expected = BuiltInFormulaReferences.DemandOscillatorValues(bars, reference, average, range, line); var batch = Data(bars).CalculateDemandOscillator(kind, average, range, line);
        Assert.Equal(expected.Outputs["Do"], batch.CustomValuesList); foreach (var key in new[] { "Do", "Signal" }) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); foreach (var signal in new[] { false, true }) { using var fast = IndicatorCompute.ComputeDemandOscillatorFast(Data(bars), context, kind, average, range, line, signal); Assert.Equal(expected.Outputs[signal ? "Signal" : "Do"], fast.ToArray()); }
        using var state = new DemandOscillatorState(kind, average, range, line); using var window = new DemandOscillatorWindow(kind, average, range, line);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in new[] { B(7, -3, 4), B(9, -7, -2), B(3, 0, 2) }) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, b.Volume, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99, -99, -7, 31)), false, false); window.Next(99, -99, -7, 31, false);
                foreach (var final in new[] { false, false, true }) { var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, b.Volume, final); Assert.Equal(expected.Outputs["Do"][i], point.Value); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Outputs["Signal"][i], point.Outputs!["Signal"]); Assert.Equal(point.Value, point.Outputs["Do"]); Assert.Equal(expected.Signals[i], direct.Trade); }
            }
        }
        return expected;
    }
    [Fact]
    public void HandSignalsDistinguishAccelerationFromSlowingInBothDirections()
    {
        var values = new[] { 0d, 4, 6, 7, 7, 3, 1, 0 }; var bars = values.Select(v => B(0, 0, 0, -v)).ToArray();
        var result = Check(bars, average: 1, range: 1, line: 1);
        Assert.Equal(values, result.Outputs["Signal"]); Assert.Equal(values, result.Outputs["Do"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.None, Signal.StrongSell, Signal.Sell, Signal.Sell }, result.Signals);
        Check(values.Select(v => B(0, 0, 0, v)).ToArray(), average: 1, range: 1, line: 1);
        Assert.Equal(new[] { 100d, 100 }, Check(new[] { B(1, 1, 1, 100), B(2, 2, 2, 100) }, average: 1, range: 1, line: 1).Outputs["Do"]);
        Assert.Equal(new[] { 2d, -3 }, Check(new[] { B(4, 0, 2, 2), B(4, 0, 2, 3) }, average: 1, range: 1, line: 1).Outputs["Do"]);
    }
    [Fact]
    public void WideSignedPricesRangesAndVolumesRetainEveryStage()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 17).Select(i => B(7 * scale, -7 * scale, (i % 9 - 4) * scale, i % 3 == 0 ? -double.MaxValue : double.MaxValue)).ToArray(), kind.Kind, kind.Ref);
        foreach (var kind in Kinds) Check(new[] { B(double.MaxValue, -double.MaxValue, -double.MaxValue), B(double.MaxValue, -double.MaxValue, double.MaxValue), B(double.MaxValue, -double.MaxValue, 0), B(4, -2, -1), B(4, -2, 2), B(4, -2, 1) }, kind.Kind, kind.Ref);
        Check(Enumerable.Range(0, 19).Select(i => B(2, -1, i % 2 == 0 ? 1 : Math.BitIncrement(1), i + 1)).ToArray());
    }
    [Fact]
    public void TinyReciprocalDenominatorsAndOverflowingHistoryRecover()
    {
        var bars = new[] { B(1, 0, 0), B(1, 0, double.Epsilon), B(1, 0, 2 * double.Epsilon), B(4, 0, 1), B(4, 0, 2), B(4, 0, 3) };
        foreach (var kind in Kinds) Check(bars, kind.Kind, kind.Ref, 2, 2, 2);
        var recovery = new[] { B(1, 0, double.Epsilon), B(1, 0, 2 * double.Epsilon) }.Concat(Enumerable.Range(0, 100).Select(i => B(4, 0, i % 2 + 1))).ToArray();
        var result = Check(recovery, average: 2, line: 2); Assert.DoesNotContain(result.Outputs["Do"], double.IsNaN); Assert.True(double.IsFinite(result.Outputs["Do"][^1]));
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<Bar>(), kind.Kind, kind.Ref, length, length, length); Check(new[] { B(4, -2, -1), B(4, -2, 2), B(7, -4, 1), B(3, -1, 0) }, kind.Kind, kind.Ref, length, length, length); }
    }
    [Fact]
    public void CoreUsesThePublicPressureFormulaAndProtectsSpanBounds()
    {
        var bars = Enumerable.Range(0, 16).Select(i => B(7, -3, i % 7 - 3, i + 1)).ToArray(); var high = bars.Select(b => b.High).ToArray(); var low = bars.Select(b => b.Low).ToArray(); var volume = bars.Select(b => b.Volume).ToArray();
        foreach (var length in new[] { 0, 1, 14, int.MaxValue })
        {
            var expected = BuiltInFormulaReferences.DemandOscillatorValues(bars, 3, 10, 2, length).Outputs["Do"]; var input = bars.Select(b => b.Close).ToArray(); var output = new double[bars.Length + 1]; output[^1] = 97;
            OscillatorCore.DemandOscillator(high, low, input, volume, output, length); Assert.Equal(expected, output.Take(input.Length)); Assert.Equal(97, output[^1]);
            OscillatorCore.DemandOscillator(high, low, input, volume, input, length); Assert.Equal(expected, input);
        }
        Assert.Throws<ArgumentException>(() => OscillatorCore.DemandOscillator(high, low, high, volume, Array.Empty<double>()));
        var unchanged = Enumerable.Repeat(97d, bars.Length).ToArray(); Assert.Throws<ArgumentException>(() => OscillatorCore.DemandOscillator(Array.Empty<double>(), low, high, volume, unchanged)); Assert.All(unchanged, v => Assert.Equal(97, v));
        Assert.Throws<ArgumentException>(() => OscillatorCore.DemandOscillator(high, Array.Empty<double>(), high, volume, unchanged)); Assert.Throws<ArgumentException>(() => OscillatorCore.DemandOscillator(high, low, high, Array.Empty<double>(), unchanged));
    }
    [Fact]
    public void LegacyKindsAndSelectedInputsKeepTheCandleRule()
    {
        var bars = Enumerable.Range(0, 15).Select(i => B(7, -3, i % 7 - 3, i + 1)).ToArray(); var kind = MovingAvgType.DoubleExponentialMovingAverage;
        var batch = Data(bars).CalculateDemandOscillator(kind, 3, 2, 4); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeDemandOscillatorFast(Data(bars), context, kind, 3, 2, 4); Assert.Equal(batch.CustomValuesList, fast.ToArray());
        using var state = new DemandOscillatorState(kind, 3, 2, 4); for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
        var selected = bars.Select((_, i) => i % 2 == 0 ? -11d : 12d).ToArray(); var data = Data(bars); data.SetCustomValues(selected.ToList()); var selectedBatch = Data(bars); selectedBatch.SetCustomValues(selected.ToList()); selectedBatch.CalculateDemandOscillator(); using var output = IndicatorCompute.ComputeDemandOscillatorFast(data, context); Assert.Equal(selectedBatch.CustomValuesList, output.ToArray()); Assert.Equal(selected, data.ChainedValues);
    }
    [Fact]
    public void FastCallbacksKeepTheirInputOrderAndBatchConsumesNone()
    {
        var bars = new[] { B(4, 0, 1, 100), B(4, 0, 2, 100), B(4, 0, 3, 100), B(4, 0, 1, 100) }; var prices = bars.Select(b => b.Close).ToArray(); var customLine = new[] { 1d, 3, -2, 4 }; var customSignal = new[] { 2d, 0, 3, 1 };
        var pressure = prices.Select((c, i) => { var prev = i == 0 ? 0 : prices[i - 1]; var pct = prev == 0 ? 0 : (c - prev) / Math.Abs(prev) * 100; var product = pct * (3 * c / 4); var inv = product == 0 ? 0 : 100 / product; return c > prev ? 100 - inv : inv - 100; }).ToArray();
        foreach (var signal in new[] { false, true })
        {
            var calls = 0; var data = Data(bars); Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { var slot = calls++; Assert.Equal(slot == 1 ? 4 : 3, period); Assert.Equal(slot == 0 ? new[] { 4d, 4, 4, 4 } : slot == 1 ? pressure : customLine, values); return slot == 0 ? new[] { 4d, 4, 4, 4 } : slot == 1 ? customLine : customSignal; };
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, signal ? 3 : 2).ToArray());
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeDemandOscillatorFast(data, context, length1: 3, length2: 2, length3: 4, signal: signal); Assert.Equal(signal ? customSignal : customLine, output.ToArray()); Assert.Equal(signal ? 3 : 2, calls); Assert.Equal(prices, data.InputValues);
        }
        foreach (var kind in Kinds)
        { var calls = 0; using var armed = ComponentAverage.Arm((values, _) => { calls++; return values; }); var expected = BuiltInFormulaReferences.DemandOscillatorValues(bars, kind.Ref, 3, 2, 4); var batch = Data(bars).CalculateDemandOscillator(kind.Kind, 3, 2, 4); Assert.Equal(expected.Outputs["Do"], batch.CustomValuesList); Assert.Equal(0, calls); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceNativeHistory()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new DemandOscillatorState(); using var control = new DemandOscillatorState(); foreach (var b in new[] { B(4, 0, 1), B(4, 0, 2) }) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(4, 0, 3), B(4, 0, 1) }) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
