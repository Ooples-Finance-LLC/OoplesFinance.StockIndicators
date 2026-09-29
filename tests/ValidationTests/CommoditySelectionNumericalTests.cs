using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CommoditySelectionNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(CommoditySelectionIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDirectionalStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.CommoditySelectionOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static IReadOnlyDictionary<string, double[]> Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int referenceKind = 1, double pointValue = 50, double margin = 3000, double commission = 10)
    {
        var expected = BuiltInFormulaReferences.CommoditySelectionValues(bars, length, referenceKind, pointValue, margin, commission); var batch = Data(bars).CalculateCommoditySelectionIndex(kind, length, pointValue, margin, commission);
        Assert.Equal(expected.Outputs["Csi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Csi", "Signal" }) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeCommoditySelectionIndexFast(Data(bars), context, length, kind, pointValue, margin, commission, key == "Signal"); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new CommoditySelectionIndexState(kind, length, pointValue, margin, commission); using var window = new CommoditySelectionWindow(kind, length, pointValue, margin, commission);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (3, -1, 2), (9, 3, 6), (2, -2, 0))) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 999))[0]), false, false); window.Next(999, -999, 999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    Assert.Equal(expected.Outputs["Csi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Csi"]); Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(point.Outputs["Signal"], direct.Signal); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideRangesDirectionalMeansAndPartialSignalMatchRationalStages()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var kind in Kinds) foreach (var length in new[] { 1, 3, 7 })
            Check(Candles(Enumerable.Range(0, 17).Select(i => ((i % 9 + 3) * scale, (i % 7 - 4) * scale, (i % 5 - 1) * scale)).ToArray()), length, kind.Kind, kind.Reference);
        Check(Candles((double.MaxValue, -double.MaxValue, 0), (0, -double.MaxValue, -double.MaxValue), (double.MaxValue, 0, double.MaxValue), (0, 0, 0), (0, 0, 0), (1, -1, 0), (2, 0, 1)), 2);
        Check(Candles((1, Math.BitDecrement(1d), 1), (Math.BitIncrement(1d), 1, 1), (1, Math.BitDecrement(1d), 1)), 2);
    }
    [Fact]
    public void HandSeedPartialMeanDirectionalTiesAndReversedSignalsArePreserved()
    {
        var bars = Candles((2, 0, 1), (3, 1, 2), (2, 0, 1)); var result = Check(bars, 1, pointValue: 1, margin: 1, commission: -50); Assert.Equal(new[] { 0d, 200, 200 }, result["Csi"]); Assert.Equal(result["Csi"], result["Signal"]);
        result = Check(bars, 2, pointValue: 1, margin: 1, commission: -50); Assert.Equal(new[] { 0d, 100, 100 }, result["Csi"]); Assert.Equal(new[] { 0d, 50, 100 }, result["Signal"]);
        using var window = new CommoditySelectionWindow(MovingAvgType.SimpleMovingAverage, 2, 1, 1, -50); Assert.Equal(Signal.None, window.Next(2, 0, 1, true).Trade); Assert.Equal(Signal.StrongSell, window.Next(3, 1, 2, true).Trade); Assert.Equal(Signal.None, window.Next(2, 0, 1, true).Trade);
        result = Check(Candles((2, 0, 1), (3, -1, 1), (4, -2, 1)), 1); Assert.All(result["Csi"], value => Assert.Equal(0, value));
        result = Check(Candles((double.MaxValue, double.MaxValue, double.MaxValue), (double.MaxValue, double.MaxValue, double.MaxValue))); Assert.All(result["Csi"], value => Assert.Equal(0, value));
    }
    [Fact]
    public void ExtremePeriodsGrowOnlyWithObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { 0, int.MaxValue })
        { Check(Candles((double.MaxValue, -double.MaxValue, 0), (3, -1, 2), (8, -2, 1), (0, 0, 0)), length, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), length, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void FiveCustomComponentsPreserveOrderSelectedRangesAndBothOutputs()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var selected = new[] { 5d, 12, 6, 15 }; var effective = Candles((9, 1, 5), (12, 5, 12), (9, 1, 6), (15, 6, 15));
        var supplied = new[] { new[] { 2d, 4, 1, 3 }, new[] { 1d, 2, 0, 3 }, new[] { 3d, 1, 2, 0 }, new[] { 4d, 5, 2, 3 }, new[] { 7d, 3, 9, -2 } };
        foreach (var custom in new[] { false, true }) foreach (var route in new[] { "batch", "fast", "signal" })
        {
            var expected = BuiltInFormulaReferences.CommoditySelectionValues(effective, 3, 1, external: custom ? supplied : null); var calls = new List<int>();
            var callbacks = Enumerable.Range(0, 5).Select(slot => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((values, period) => { Assert.Equal(3, period); Assert.Equal(expected.Components[slot], values); calls.Add(slot); return supplied[slot]; })).ToArray();
            using var armed = custom ? ComponentAverage.Arm(callbacks) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (route == "batch") { data.CalculateCommoditySelectionIndex(MovingAvgType.SimpleMovingAverage, 3); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); }
            else { using var output = IndicatorCompute.ComputeCommoditySelectionIndexFast(data, context, 3, MovingAvgType.SimpleMovingAverage, signal: route == "signal"); Assert.Equal(expected.Outputs[route == "signal" ? "Signal" : "Csi"], output.ToArray()); }
            Assert.Equal(custom ? new[] { 0, 1, 2, 3, 4 } : Array.Empty<int>(), calls); if (custom) Assert.Equal(5, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void TinyCoefficientRetainsRangeAndExtendedSignalCanRecover()
    {
        var result = Check(Candles((double.MaxValue / 2, -double.MaxValue / 2, 0), (double.MaxValue, 0, double.MaxValue / 2)), 1, pointValue: double.Epsilon, margin: 1, commission: double.MaxValue); Assert.True(result["Csi"][1] > 0);
        foreach (var point in new[] { 0d, -2d, double.MaxValue }) Check(Candles((2, 0, 1), (3, 1, 2), (1, -1, 0)), 2, pointValue: point, margin: double.Epsilon, commission: -151);
        using var window = new CommoditySelectionWindow(MovingAvgType.SimpleMovingAverage, 2, double.MaxValue, 1, -50, true);
        Assert.True(double.IsPositiveInfinity(window.Next(0, 0, 0, true, double.MaxValue, 100).Value));
        Assert.Equal(0, window.Next(0, 0, 0, true, 0, 100).Value);
        var recovered = window.Next(0, 0, 0, true, 1, 1); Assert.Equal(double.MaxValue, recovered.Value); Assert.Equal(double.MaxValue / 2, recovered.Signal); Assert.Equal(Signal.StrongSell, recovered.Trade);
    }
    [Fact]
    public void LegacyAveragesPreserveBatchNativeAndFastParity()
    {
        var bars = Candles((9, 1, 2), (4, 2, 3), (8, 3, 4), (6, 1, 2));
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateCommoditySelectionIndex(kind, 3); using var state = new CommoditySelectionIndexState(kind, 3); using var context = new ComputeContext();
            foreach (var signal in new[] { false, true }) { using var output = IndicatorCompute.ComputeCommoditySelectionIndexFast(Data(bars), context, 3, kind, signal: signal); Assert.Equal(batch.OutputValues[signal ? "Signal" : "Csi"], output.ToArray()); }
            for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); Assert.Equal(batch.CustomValuesList[i], point.Value); Assert.Equal(batch.OutputValues["Signal"][i], point.Outputs!["Signal"]); }
        }
    }
    [Fact]
    public void InvalidScaleParametersRejectBeforeCallbacks()
    {
        var invalid = new[] { (double.NaN, 1d, 0d), (double.PositiveInfinity, 1d, 0d), (1d, 0d, 0d), (1d, -1d, 0d), (1d, double.NaN, 0d), (1d, double.PositiveInfinity, 0d), (1d, 1d, -150d), (1d, 1d, double.NaN), (1d, 1d, double.NegativeInfinity) };
        foreach (var (point, margin, fee) in invalid)
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (v, _) => { calls++; return v; }; using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext(); var bars = Candles((2, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommoditySelectionIndexState(pointValue: point, margin: margin, commission: fee)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateCommoditySelectionIndex(pointValue: point, margin: margin, commission: fee)); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeCommoditySelectionIndexFast(Data(bars), context, pointValue: point, margin: margin, commission: fee)); Assert.Equal(0, calls);
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new CommoditySelectionIndexState(length: 3); using var control = new CommoditySelectionIndexState(length: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
