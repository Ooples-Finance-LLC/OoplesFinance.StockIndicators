using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class BilateralStochasticNumericalTests
{
    private static readonly string[] Keys = { "Bull", "Bear", "Bso", "Signal" };
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BSO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BilateralStochasticOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeMeans(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.BilateralOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 3, int signalLength = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.BilateralValues(bars, length, signalLength, Kind(kind)); var batch = Data(bars).CalculateBilateralStochasticOscillator(kind, length, signalLength); Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs["Bso"], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in Keys) { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var output = IndicatorCompute.ComputeBilateralStochasticOscillatorFast(Data(bars), context, length, kind, key, signalLength); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new BilateralStochasticOscillatorState(kind, length, signalLength);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 8d, -3, 4, 1, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Bso"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) External(double[] mean, double[] scale, double[] signal, int length = 3)
    {
        var bars = Bars(Enumerable.Range(0, mean.Length).Select(i => (double)i)); var supplied = new[] { mean, scale, signal }; var expected = BuiltInFormulaReferences.BilateralValues(bars, length, 2, 1, supplied);
        foreach (var route in Keys.Append("batch"))
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(calls < 2 ? length : 2, period); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext();
            if (route == "batch") { var batch = Data(bars).CalculateBilateralStochasticOscillator(length: length, signalLength: 2); foreach (var key in Keys) Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Signals, batch.SignalsList); }
            else { using var output = IndicatorCompute.ComputeBilateralStochasticOscillatorFast(Data(bars), context, length, outputKey: route, signalLength: 2); Assert.Equal(expected.Outputs[route], output.ToArray()); } Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Substitutions);
        }
        return expected;
    }
    [Fact]
    public void WideMeansRangesAndSignalStagesMatchRationalArithmetic()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { 1, 3, 7 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Bars(new[] { 2d, 4, 6, 2, -4, -6, 3, 0, 2, 7, -5, 1, 1, 0, 4, 2 }.Select(p => p * scale)), length, 2, kind);
    }
    [Fact]
    public void HandOutputsPreserveWarmupAndSignalSmoothing()
    {
        var output = Check(Bars(new[] { 1d, 2, 1 }), 2, 2); Assert.Equal(new[] { 0d, 2, 0 }, output["Bull"]); Assert.Equal(new[] { 0d, 0, 0 }, output["Bear"]); Assert.Equal(output["Bull"], output["Bso"]); Assert.Equal(new[] { 0d, 1, 1 }, output["Signal"]);
        foreach (var kind in Kinds) foreach (var values in Check(Bars(new[] { 1d, 3, -2, 7, 0, 4 }), 1, 1, kind).Values) Assert.All(values, v => Assert.Equal(0, v));
    }
    [Fact]
    public void ExtendedRangesAndExactNumeratorsAvoidOverflowAndCancellation()
    {
        var max = double.MaxValue; var values = External(new[] { max, max, -max, 0 }, new[] { max, max, max, max }, new double[4], 4).Outputs;
        Assert.Equal(new[] { 0d, 0, 0, 1 }, values["Bull"]); Assert.Equal(new[] { 0d, 0, 2, 1 }, values["Bear"]);
        foreach (var kind in Kinds) Check(Bars(new[] { max, -max, max, 0d, -max, 0, max }), 2, 3, kind);
        Check(Bars(new[] { max, max, max, -max, -max, -max, max, max, max, 0d }), 3, 2);
        var adjacent = Math.BitIncrement(1d); var tiny = adjacent - 1; var close = External(new[] { 1d, adjacent, 1 }, new[] { tiny, tiny, tiny }, new double[3]).Outputs; Assert.Equal(new[] { 0d, 1, 0 }, close["Bull"]); Assert.Equal(new[] { 0d, 0, 1 }, close["Bear"]);
        var thirds = External(new[] { 1d, adjacent, 1 }, new[] { 3 * tiny, 3 * tiny, 3 * tiny }, new double[3]).Outputs; Assert.Equal(new[] { 0d, 1d / 3, 0 }, thirds["Bull"]); Assert.Equal(new[] { 0d, 0, 1d / 3 }, thirds["Bear"]);
    }
    [Fact]
    public void ZeroScaleEqualityAndBuyPriorityKeepConditionSemantics()
    {
        var output = External(new[] { 0d, 2, .5 }, new[] { 1d, 1, 1 }, new double[3]); Assert.Equal(new[] { Signal.None, Signal.Buy, Signal.Buy }, output.Signals); Assert.Equal(new[] { 0d, 0, 1.5 }, output.Outputs["Bear"]);
        var equal = External(new[] { 0d, 2, 1, 1 }, new[] { 1d, 1, 1, 1 }, new[] { 0d, 0, 2, 1 }); Assert.Equal(new[] { Signal.None, Signal.Buy, Signal.Sell, Signal.Sell }, equal.Signals);
        var zero = External(new[] { -3d, 2, -1 }, new double[3], new double[3]); foreach (var values in zero.Outputs.Values) Assert.All(values, v => Assert.Equal(0, v)); Assert.All(zero.Signals, v => Assert.Equal(Signal.None, v));
        var negative = External(new[] { 0d, 2, 1 }, new[] { -1d, -1, -1 }, new double[3]); Assert.Equal(new[] { 0d, -2, -1 }, negative.Outputs["Bull"]); Assert.Equal(new[] { 0d, 0, 1 }, negative.Outputs["Bear"]);
    }
    [Fact]
    public void ExtremaExpireAndPowerOfTwoScalingPreservesRatios()
    {
        Assert.Equal(new[] { 0d, 100, 0, 1 }, External(new[] { 0d, 100, 1, 2 }, new[] { 1d, 1, 1, 1 }, new double[4], 2).Outputs["Bull"]);
        var prices = new[] { 2d, 4, 6, 2, -4, 1, 5, 0, 3, -1 }; var expected = Check(Bars(prices)); foreach (var factor in new[] { Math.Pow(2, -600), Math.Pow(2, 600) }) { var actual = Check(Bars(prices.Select(v => v * factor))); foreach (var key in Keys) Assert.Equal(expected[key], actual[key]); }
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepAllThreeAveragesLazy()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds) foreach (var lengths in new[] { (period, 3), (3, period) }) foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }) }) Check(bars, lengths.Item1, lengths.Item2, kind);
    }
    [Fact]
    public void CustomAndLegacyComponentsKeepInputRangeMaximumOrder()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8 }); var selected = new[] { -2d, 0, 4, 3 }; var supplied = new[] { new[] { 0d, 2, .5, 1 }, new[] { 1d, 1, 1, 1 }, new[] { 0d, 0, 2, 1 } }; var expected = BuiltInFormulaReferences.BilateralValues(Bars(selected), 3, 2, 1, supplied);
        foreach (var route in Keys.Append("batch"))
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(calls < 2 ? 3 : 2, period); Assert.Equal(calls == 0 ? selected : calls == 1 ? new[] { 0d, 2, 2, 1.5 } : expected.Outputs["Bso"], values); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            if (route == "batch") { data.CalculateBilateralStochasticOscillator(length: 3, signalLength: 2); foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); } else { using var output = IndicatorCompute.ComputeBilateralStochasticOscillatorFast(data, context, 3, outputKey: route, signalLength: 2); Assert.Equal(expected.Outputs[route], output.ToArray()); } Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Substitutions);
        }
        var selectedReference = BuiltInFormulaReferences.BilateralValues(Bars(selected), 3, 2, 1).Outputs; using var ctx = new ComputeContext(); foreach (var key in Keys) { var data = Data(bars); data.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeBilateralStochasticOscillatorFast(data, ctx, 3, outputKey: key, signalLength: 2); Assert.Equal(selectedReference[key], output.ToArray()); }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var legacy = Data(bars).CalculateBilateralStochasticOscillator(kind, 3, 2); using var native = new BilateralStochasticOscillatorState(kind, 3, 2); foreach (var key in Keys) { using var output = IndicatorCompute.ComputeBilateralStochasticOscillatorFast(Data(bars), ctx, 3, kind, key, 2); Assert.Equal(legacy.OutputValues[key], output.ToArray()); } for (var i = 0; i < bars.Length; i++) { var point = native.Update(Native(bars[i]), true, true); foreach (var key in Keys) Assert.Equal(legacy.OutputValues[key][i], point.Outputs![key]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMeansExtremaOrSignal()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new BilateralStochasticOscillatorState(length: 3, signalLength: 2); using var control = new BilateralStochasticOscillatorState(length: 3, signalLength: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var a = state.Update(Native(bar), true, true); var b = control.Update(Native(bar), true, true); Assert.Equal(b.Value, a.Value); foreach (var key in Keys) Assert.Equal(b.Outputs![key], a.Outputs![key]); }
        }
    }
}
