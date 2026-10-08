using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FisherStochasticNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FisherTransformStochasticOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRainbowAndLogistic(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FisherStochOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static Bar[] Prices(params double[] values) => Candles(values.Select(v => (v, v, v)).ToArray());
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 2, int range = 4, int smooth = 3,
        MovingAvgType kind = MovingAvgType.WeightedMovingAverage, int reference = 2)
    {
        var expected = BuiltInFormulaReferences.FisherStochValues(bars, length, range, smooth, reference); var batch = Data(bars).CalculateFisherTransformStochasticOscillator(kind, length, range, smooth);
        Assert.Equal(expected.Outputs["Ftso"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFisherTransformStochasticOscillatorFast(Data(bars), context, length, range, smooth, kind); Assert.Equal(expected.Outputs["Ftso"], output.ToArray());
        using var state = new FisherTransformStochasticOscillatorState(kind, length, range, smooth); using var window = new FisherStochasticWindow(kind, length, range, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Prices(8, -2, 6, 1)) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Prices(999)[0]), false, false); window.Next(-999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final);
                    Assert.Equal(expected.Outputs["Ftso"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ftso"]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandRegularizationMidpointAndFlatEndpoint()
    {
        var floor = 100 / (1 + Math.Exp(10)); var result = Check(Prices(0, .0001, 0, .0001), 1, 2, 1);
        Assert.Equal(new[] { floor, 50, floor, 50 }, result.Outputs["Ftso"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongBuy, Signal.StrongSell, Signal.StrongBuy }, result.Signals);
        Assert.All(Check(Prices(4, -2, 7, 0), 1, 1, 3).Outputs["Ftso"], v => Assert.Equal(floor, v));
        Assert.All(Check(Prices(0, 0, 0, 0)).Outputs["Ftso"], v => Assert.Equal(floor, v));
        Check(Prices(0, .0001, 0, .0001, .0001, 0), 1, 2, 2);
        Assert.Equal(double.MaxValue, FisherStochasticWindow.Rainbow(Enumerable.Repeat(new RocBankValue(double.MaxValue), 10).ToArray()).Publish());
        Assert.Equal(double.Epsilon, FisherStochasticWindow.Rainbow(Enumerable.Repeat(new RocBankValue(double.Epsilon), 10).ToArray()).Publish());
    }
    [Fact]
    public void WideRainbowRangesAndTenMeansMatchFractions()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Prices(Enumerable.Range(0, 19).Select(i => (i % 7 - 3) * scale).ToArray()), kind: kind.Kind, reference: kind.Reference);
        foreach (var kind in Kinds)
        {
            Check(Prices(double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue, -double.MaxValue, 1, 1, 1, 1, 1, 1), kind: kind.Kind, reference: kind.Reference);
            var constant = Check(Prices(Enumerable.Repeat(double.MaxValue, 32).ToArray()), kind: kind.Kind, reference: kind.Reference); if (kind.Reference != 6) Assert.Equal(100 / (1 + Math.Exp(10)), constant.Outputs["Ftso"][^1]);
            Check(Prices(1, Math.BitIncrement(1), 1, Math.BitIncrement(1), 1), kind: kind.Kind, reference: kind.Reference);
        }
    }
    [Fact]
    public void RangeExpirySumExpiryAndTiesKeepSeparateHistories()
    {
        var prices = Prices(0, 8, 8, -3, -3, 2, 1, 1, -1, 4, 4, 0, 0, 0, 0, 0);
        foreach (var range in new[] { 2, 3, 5 }) foreach (var smooth in new[] { 1, 2, 7 }) Check(prices, 1, range, smooth);
        var recovery = Check(Prices(-double.MaxValue, double.MaxValue, -double.MaxValue, 1, 1, 1, 1, 1, 1), 1, 2, 2); Assert.Equal(100 / (1 + Math.Exp(10)), recovery.Outputs["Ftso"][^1]);
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedBars()
    {
        foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var slot in Enumerable.Range(0, 3))
        { var periods = new[] { 2, 3, 2 }; periods[slot] = period; Check(Prices(4, -2, 5), periods[0], periods[1], periods[2], kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), periods[0], periods[1], periods[2], kind.Kind, kind.Reference); }
    }
    [Fact]
    public void TenCallbackSlotsPreserveStageInputsAndSelectedPrices()
    {
        var bars = Prices(1, 4, -2, 8, 0); var selected = new[] { 4d, -1, 3, 7, 2 }; var data = Data(bars); data.SetCustomValues(selected.ToList());
        var supplied = Enumerable.Range(0, 10).Select(depth => new[] { 1d + depth, 2 - depth, 3d * depth, -1, 4 - depth }).ToArray();
        var expected = BuiltInFormulaReferences.FisherStochValues(Prices(selected), 3, 2, 4, 2, supplied); var calls = 0;
        using (ComponentAverage.Arm(Enumerable.Range(0, 10).Select(slot => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, n) => { Assert.Equal(3, n); Assert.Equal(slot == 0 ? selected : supplied[slot - 1], values); calls++; return supplied[slot]; })).ToArray()))
        { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFisherTransformStochasticOscillatorFast(data, context, 3, 2, 4); Assert.Equal(expected.Outputs["Ftso"], output.ToArray()); Assert.Equal(10, calls); }
        Assert.Equal(selected, data.ChainedValues); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        using (ComponentAverage.Arm((v, _) => { calls++; return v; }))
        { var batch = Data(Prices(selected)).CalculateFisherTransformStochasticOscillator(length: 3, stochLength: 2, smoothLength: 4); Assert.Equal(BuiltInFormulaReferences.FisherStochValues(Prices(selected), 3, 2, 4, 2).Outputs["Ftso"], batch.CustomValuesList); Assert.Equal(0, ComponentAverage.Requests); }
        var wide = Prices(double.MaxValue, -double.MaxValue, double.MaxValue, 0, 0, 0, 0); calls = 0;
        using (ComponentAverage.Arm((v, _) => { calls++; return v; }))
        { Assert.NotNull(ComponentAverage.Take(new[] { 1d }, 1)); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFisherTransformStochasticOscillatorFast(Data(wide), context, 3, 2, 4); Assert.Equal(BuiltInFormulaReferences.FisherStochValues(wide, 3, 2, 4, 2).Outputs["Ftso"], output.ToArray()); Assert.Equal(1, calls); Assert.Equal(11, ComponentAverage.Requests); }
    }
    [Fact]
    public void LegacyMeansKeepPublicRouteAgreement()
    {
        var bars = Prices(4, -2, 7, 0, 3, 1); var kind = MovingAvgType.TripleExponentialMovingAverage;
        var batch = Data(bars).CalculateFisherTransformStochasticOscillator(kind, 3, 2, 4); using var state = new FisherTransformStochasticOscillatorState(kind, 3, 2, 4); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeFisherTransformStochasticOscillatorFast(Data(bars), context, 3, 2, 4, kind); Assert.Equal(batch.CustomValuesList, output.ToArray());
        for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FisherTransformStochasticOscillatorState(length: 3, stochLength: 2, smoothLength: 4); using var control = new FisherTransformStochasticOscillatorState(length: 3, stochLength: 2, smoothLength: 4);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Ftso"], actual.Outputs!["Ftso"]); }
        }
    }
}
