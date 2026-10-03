using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ErgodicSelectionNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ErgodicCommoditySelectionIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDirectionalStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ErgodicSelectionOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 3, int smooth = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, double pointValue = 1)
    {
        var expected = BuiltInFormulaReferences.ErgodicSelectionValues(bars, length, smooth, reference, pointValue);
        var batch = Data(bars).CalculateErgodicCommoditySelectionIndex(kind, length, smooth, pointValue);
        Assert.Equal(expected.Outputs["Ecsi"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Ecsi", "Signal" }) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeErgodicCommoditySelectionIndexFast(Data(bars), context, length, smooth, pointValue, kind, key == "Signal"); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new ErgodicCommoditySelectionIndexState(kind, length, smooth, pointValue); using var window = new ErgodicSelectionWindow(kind, length, smooth, pointValue);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (3, -1, 2), (9, 3, 6))) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 999))[0]), false, false); window.Next(999, -999, 999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    Assert.Equal(expected.Outputs["Ecsi"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ecsi"]); Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]); Assert.Equal(point.Value, direct.Line); Assert.Equal(point.Outputs["Signal"], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return (expected.Outputs, expected.Signals);
    }
    [Fact]
    public void HandDirectionGapsPriceDomainAndSuccessiveSlopes()
    {
        var result = Check(Candles((2, 0, 1), (3, 1, 2), (2, 0, 1)), 1, 1, pointValue: 151);
        Assert.Equal(new[] { 0d, 5000, 20000 }, result.Outputs["Ecsi"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongBuy }, result.Signals);
        result = Check(Candles((11, 9, 10), (3, 1, 2)), 1, 1, pointValue: 151); Assert.Equal(22500, result.Outputs["Ecsi"][1]);
        result = Check(Candles((3, 1, 2), (11, 9, 10)), 1, 1, pointValue: 151); Assert.Equal(4500, result.Outputs["Ecsi"][1]);
        result = Check(Candles((2, 0, 1), (3, -1, 1), (4, -2, 1)), 1, 1); Assert.All(result.Outputs["Ecsi"], v => Assert.Equal(0, v));
        result = Check(Candles((2, 0, 0), (3, -1, -1), (4, 0, 0)), 1, 1); Assert.All(result.Outputs["Ecsi"], v => Assert.Equal(0, v));
        using var window = new ErgodicSelectionWindow(MovingAvgType.SimpleMovingAverage, 1, 1, 151, true);
        var signals = new[] { 0d, 4, 6, 7, 7, 3, 1, 0 }.Select(v => window.Next(2, 0, 1, true, 100, v).Trade).ToArray();
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.Buy, Signal.Buy, Signal.None, Signal.StrongSell, Signal.Sell, Signal.Sell }, signals);
    }
    [Fact]
    public void WideRangesAndDirectionalStagesMatchIndependentFractions()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var kind in Kinds) foreach (var length in new[] { 1, 3, 7 })
            Check(Candles(Enumerable.Range(0, 17).Select(i => ((i % 9 + 3) * scale, (i % 7 - 4) * scale, (i % 5 - 1) * scale)).ToArray()), length, 3, kind.Kind, kind.Reference);
        Check(Candles((double.MaxValue, -double.MaxValue, double.MaxValue / 4), (0, -double.MaxValue, 0), (double.MaxValue, 0, double.MaxValue), (0, 0, 0), (1, -1, 1), (2, 0, 1)), 2);
        Check(Candles((1, Math.BitDecrement(1d), 1), (Math.BitIncrement(1d), 1, 1), (1, Math.BitDecrement(1d), 1)), 2);
        foreach (var point in new[] { 0d, -2d, double.MaxValue }) Check(Candles((2, 0, 1), (3, 1, 2), (1, -1, 0)), 2, pointValue: point);
    }
    [Fact]
    public void TinyScaleRecoversAndExtendedLineReachesFiniteSignal()
    {
        var tiny = Check(Candles((1, 0, double.Epsilon), (2, 0, double.Epsilon)), 1, int.MaxValue, pointValue: double.Epsilon);
        Assert.Equal(10000d / (150L + int.MaxValue), tiny.Outputs["Ecsi"][1]);
        var wide = Check(Candles((1, 1, 1), (1.125, 1, 1)), 1, 2, pointValue: double.MaxValue / 4);
        Assert.True(double.IsPositiveInfinity(wide.Outputs["Ecsi"][1])); Assert.True(double.IsFinite(wide.Outputs["Signal"][1])); Assert.True(wide.Outputs["Signal"][1] > double.MaxValue / 2);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { 0, int.MaxValue }) foreach (var smooth in new[] { 0, int.MaxValue })
        { Check(Candles((double.MaxValue, -double.MaxValue, 1), (3, -1, 2), (8, -2, 1), (0, 0, 0)), length, smooth, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), length, smooth, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void FourDirectionalOverridesAndOptionalSignalKeepTheirOrder()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var selected = new[] { 5d, 12, 6, 15 }; var effective = Candles((9, 1, 5), (12, 5, 12), (9, 1, 6), (15, 6, 15));
        var supplied = new[] { new[] { 1d, 2, 0, 3 }, new[] { 3d, 1, 2, 0 }, new[] { 4d, 5, 2, 3 }, new[] { 7d, 3, 9, -2 }, new[] { 1d, 2, 3, 4 } };
        foreach (var custom in new[] { false, true }) foreach (var route in new[] { "batch", "fast", "signal", "signal-default" })
        {
            var expected = BuiltInFormulaReferences.ErgodicSelectionValues(effective, 3, 2, 1, external: custom ? supplied.Take(route == "signal" ? 5 : 4).ToArray() : null); var calls = new List<int>();
            var callbacks = Enumerable.Range(0, 5).Select(slot => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((values, period) => { Assert.Equal(slot == 4 ? 2 : 3, period); Assert.Equal(expected.Components[slot], values); calls.Add(slot); return supplied[slot]; })).ToArray();
            using var armed = custom ? ComponentAverage.Arm(route == "signal-default" ? callbacks.Take(4).ToArray() : callbacks) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (route == "batch") { data.CalculateErgodicCommoditySelectionIndex(MovingAvgType.SimpleMovingAverage, 3, 2); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); }
            else { using var output = IndicatorCompute.ComputeErgodicCommoditySelectionIndexFast(data, context, 3, 2, maType: MovingAvgType.SimpleMovingAverage, signal: route.StartsWith("signal")); Assert.Equal(expected.Outputs[route.StartsWith("signal") ? "Signal" : "Ecsi"], output.ToArray()); }
            Assert.Equal(custom ? Enumerable.Range(0, route == "signal" ? 5 : 4).ToArray() : Array.Empty<int>(), calls);
        }
    }
    [Fact]
    public void LegacyAveragesKeepBothOutputRoutes()
    {
        var bars = Candles((9, 1, 2), (4, 2, 3), (8, 3, 4), (6, 1, 2));
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateErgodicCommoditySelectionIndex(kind, 3, 2); using var state = new ErgodicCommoditySelectionIndexState(kind, 3, 2); using var context = new ComputeContext();
            foreach (var signal in new[] { false, true }) { using var output = IndicatorCompute.ComputeErgodicCommoditySelectionIndexFast(Data(bars), context, 3, 2, maType: kind, signal: signal); Assert.Equal(batch.OutputValues[signal ? "Signal" : "Ecsi"], output.ToArray()); }
            for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); Assert.Equal(batch.CustomValuesList[i], point.Value); Assert.Equal(batch.OutputValues["Signal"][i], point.Outputs!["Signal"]); }
        }
    }
    [Fact]
    public void InvalidPointValueRejectsBeforeAnyCallback()
    {
        foreach (var point in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (v, _) => { calls++; return v; }; using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext(); var bars = Candles((2, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ErgodicCommoditySelectionIndexState(pointValue: point)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateErgodicCommoditySelectionIndex(pointValue: point)); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeErgodicCommoditySelectionIndexFast(Data(bars), context, pointValue: point)); Assert.Equal(0, calls);
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new ErgodicCommoditySelectionIndexState(length: 3); using var control = new ErgodicCommoditySelectionIndexState(length: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
