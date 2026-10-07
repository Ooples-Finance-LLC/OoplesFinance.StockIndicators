using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FxSniperNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FXSniperIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDirectionalStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FxSniperOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int cciLength = 3, int t3Length = 5, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, double factor = .618)
    {
        var prices = bars.Select(b => CommodityIndexWindow.TypicalPrice(b.High, b.Low, b.Close)).ToArray();
        var expected = BuiltInFormulaReferences.FxSniperValues(prices, cciLength, t3Length, reference, factor);
        var batch = Data(bars).CalculateFXSniperIndicator(kind, cciLength, t3Length, factor);
        Assert.Equal(expected.Outputs["FXSniper"], batch.CustomValuesList); Assert.Equal(expected.Outputs["FXSniper"], batch.OutputValues["FXSniper"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFXSniperFast(Data(bars), context, cciLength, t3Length, factor, kind); Assert.Equal(expected.Outputs["FXSniper"], output.ToArray());
        using var state = new FXSniperIndicatorState(kind, cciLength, t3Length, factor); using var window = new FxSniperWindow(kind, cciLength, t3Length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (3, -1, 2), (9, 3, 6))) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 999))[0]), false, false); window.Next(999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(prices[i], final);
                    Assert.Equal(expected.Outputs["FXSniper"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["FXSniper"]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return (expected.Outputs, expected.Signals);
    }
    [Fact]
    public void HandIdentityAndThirdOrSixthPoleOpeningValues()
    {
        var bars = Candles((3, 1, 2), (5, 3, 4), (3, 1, 2)); var cci = new[] { 0d, 200d / 3, -200d / 3 };
        foreach (var factor in new[] { 0d, -1d, .618, 1d, double.MaxValue, -double.MaxValue })
            Assert.Equal(cci, Check(bars, 2, 1, factor: factor).Outputs["FXSniper"]);
        var third = Check(bars, 2, 5, factor: 0); Assert.Equal(cci[1] / 8, third.Outputs["FXSniper"][1]);
        var sixth = Check(bars, 2, 5, factor: -1); Assert.Equal(cci[1] / 64, sixth.Outputs["FXSniper"][1]);
        Assert.Equal(new[] { Signal.None, Signal.StrongBuy, Signal.StrongSell }, Check(bars, 2, 1).Signals);
        Assert.All(Check(Candles((2, 2, 2), (2, 2, 2), (2, 2, 2))).Outputs["FXSniper"], v => Assert.Equal(0, v));
    }
    [Fact]
    public void WidePricesResidualsAndPolynomialProductsMatchFractions()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var kind in Kinds) foreach (var length in new[] { 1, 3, 7 })
            Check(Candles(Enumerable.Range(0, 17).Select(i => ((i % 9 + 3) * scale, (i % 7 - 4) * scale, (i % 5 - 1) * scale)).ToArray()), length, 4, kind.Kind, kind.Reference);
        foreach (var factor in new[] { double.Epsilon, -2d, double.MaxValue / 2 })
            Check(Candles((double.MaxValue, double.MaxValue, double.MaxValue), (-double.MaxValue, -double.MaxValue, -double.MaxValue), (double.MaxValue, -double.MaxValue, 0), (1, -1, 0)), 2, 3, factor: factor);
        Check(Candles((1, Math.BitDecrement(1d), 1), (Math.BitIncrement(1d), 1, 1), (1, Math.BitDecrement(1d), 1)), 2);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedHistory()
    {
        foreach (var kind in Kinds) foreach (var cci in new[] { 0, int.MaxValue }) foreach (var t3 in new[] { 0, int.MaxValue })
        { Check(Candles((double.MaxValue, -double.MaxValue, 1), (3, -1, 2), (8, -2, 1), (0, 0, 0)), cci, t3, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), cci, t3, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void SelectedInputAndComponentMeansKeepBatchAndFastContracts()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var prices = new[] { 5d, 12, 6, 15 };
        var supplied = new[] { new[] { 1d, 2, 3, 4 }, new[] { 2d, 3, 4, 5 } };
        foreach (var kind in Kinds) foreach (var route in new[] { "batch", "fast" }) foreach (var custom in new[] { false, true })
        {
            var applied = custom && route == "fast" && kind.Reference != 1;
            var expected = BuiltInFormulaReferences.FxSniperValues(prices, 3, 5, kind.Reference, external: applied ? supplied : null); var calls = 0;
            var callbacks = Enumerable.Range(0, 2).Select(slot => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((values, period) => { Assert.Equal(3, period); Assert.Equal(slot == 0 ? prices : prices.Select((v, i) => Math.Abs(v - supplied[0][i])).ToArray(), values); calls++; return supplied[slot]; })).ToArray();
            using var armed = custom ? ComponentAverage.Arm(callbacks) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(prices.ToList());
            if (route == "batch") { data.CalculateFXSniperIndicator(kind.Kind, 3, 5, .618); Assert.Equal(expected.Outputs["FXSniper"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
            else { using var output = IndicatorCompute.ComputeFXSniperFast(data, context, 3, 5, .618, kind.Kind); Assert.Equal(expected.Outputs["FXSniper"], output.ToArray()); }
            Assert.Equal(applied ? 2 : 0, calls);
        }
        using var state = new FXSniperIndicatorState(cciLength: 3, b: .618); ((ICustomInputConsumer)state).ReadCloseAsInput();
        var selectedExpected = BuiltInFormulaReferences.FxSniperValues(prices, 3, 5, 1).Outputs["FXSniper"];
        for (var replay = 0; replay < 2; replay++) { state.Reset(); for (var i = 0; i < bars.Length; i++) { var b = bars[i]; var selected = new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume); Assert.Equal(selectedExpected[i], state.Update(Native(selected), true, false).Value); } }
    }
    [Fact]
    public void ExtendedCustomCciCanRecoverBeforePublication()
    {
        var prices = new[] { 1d, 2, 3 }; var external = new[] { new double[3], Enumerable.Repeat(double.Epsilon, 3).ToArray() };
        var expected = BuiltInFormulaReferences.FxSniperValues(prices, 3, int.MaxValue, 3, 0, external).Outputs["FXSniper"];
        Assert.All(expected, v => Assert.True(double.IsFinite(v) && v > 0));
        var callbacks = external.Select(v => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((_, _) => v)).ToArray(); using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
        var data = Data(Candles(prices.Select(v => (v, v, v)).ToArray())); using var output = IndicatorCompute.ComputeFXSniperFast(data, context, 3, int.MaxValue, 0, MovingAvgType.ExponentialMovingAverage); Assert.Equal(expected, output.ToArray());
    }
    [Fact]
    public void LegacyAveragesKeepBatchNativeAndFastParity()
    {
        var bars = Candles((9, 1, 2), (4, 2, 3), (8, 3, 4), (6, 1, 2));
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateFXSniperIndicator(kind, 3, 5, .618); using var state = new FXSniperIndicatorState(kind, 3, 5, .618); using var context = new ComputeContext();
            using var output = IndicatorCompute.ComputeFXSniperFast(Data(bars), context, 3, 5, .618, kind); Assert.Equal(batch.CustomValuesList, output.ToArray());
            for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
        }
    }
    [Fact]
    public void InvalidFactorRejectsBeforeAnyComponentCallback()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (v, _) => { calls++; return v; }; using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext(); var bars = Candles((2, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FXSniperIndicatorState(b: factor)); Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateFXSniperIndicator(b: factor)); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeFXSniperFast(Data(bars), context, b: factor)); Assert.Equal(0, calls);
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FXSniperIndicatorState(cciLength: 3); using var control = new FXSniperIndicatorState(cciLength: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["FXSniper"], actual.Outputs!["FXSniper"]); }
        }
    }
}
