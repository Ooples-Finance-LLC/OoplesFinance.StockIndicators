using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EnhancedIndexNumericalTests
{
    private static Bar B(double high, double low, double close) => new(DateTime.UnixEpoch, close, high, low, close, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EnhancedIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentNormalizedSpread(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.EnhancedIndexOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string,double[]> Outputs, Signal[] Signals) Check(Bar[] bars, int length = 5, int signalLength = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1)
    {
        var expected = BuiltInFormulaReferences.EnhancedIndexValues(bars, length, signalLength, reference); var batch = Data(bars).CalculateEnhancedIndex(kind, length, signalLength);
        Assert.Equal(expected.Outputs["Ei"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Ei", "Signal" })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEnhancedIndexFast(Data(bars), context, length, kind, signalLength, key == "Signal"); Assert.Equal(expected.Outputs[key], fast.ToArray()); }
        using var state = new EnhancedIndexState(kind, length, signalLength); using var window = new EnhancedIndexWindow(kind, length, signalLength);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(99, -99, -77)), false, false); window.Next(99, -99, -77, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    Assert.Equal(expected.Outputs["Ei"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ei"]); Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]);
                    Assert.Equal(point.Value, direct.Line); Assert.Equal(point.Outputs["Signal"], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandSeedFlatRangeAndSignedRangeHaveKnownValues()
    {
        var result = Check(new[] { B(3, 1, 2), B(5, 3, 4), B(3, 1, 2) }, 2, 2);
        Assert.Equal(new[] { 2d, .5, -.5 }, result.Outputs["Ei"]); Assert.Equal(new[] { 0d, 1.25, 0 }, result.Outputs["Signal"]);
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongSell, Signal.Sell }, result.Signals);
        Assert.All(Check(new[] { B(2, 2, 7), B(2, 2, -3), B(2, 2, 0) }, 1).Outputs["Ei"], x => Assert.Equal(0, x));
        Assert.Equal(-2, Check(new[] { B(1, 3, 2) }, 1).Outputs["Ei"][0]);
    }
    [Fact]
    public void WideTinyAndExpiredRangesRetainNormalizedValues()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -500), 1d, double.MaxValue / 8 })
            Check(Enumerable.Range(0, 19).Select(i => B((i % 5 + 2) * scale, (i % 5 - 2) * scale, (i % 5 - 1) * scale)).ToArray(), 5, 3, kind.Kind, kind.Reference);
        foreach (var kind in Kinds)
            Check(new[] { B(double.MaxValue, -double.MaxValue, double.MaxValue), B(double.MaxValue, -double.MaxValue, -double.MaxValue), B(2, -2, 1), B(3, -1, 2), B(1, 0, 1), B(9, -2, 4) }, 2, 2, kind.Kind, kind.Reference);
    }
    [Fact]
    public void ExtendedLineCanCancelBeforeSignalPublication()
    {
        var result = Check(new[] { B(double.Epsilon, 0, double.MaxValue), B(double.Epsilon, 0, -double.MaxValue), B(1, -1, 0), B(1, -1, 0), B(1, -1, 0) }, 2, 2);
        Assert.Equal(double.PositiveInfinity, result.Outputs["Ei"][0]); Assert.Equal(double.NegativeInfinity, result.Outputs["Ei"][1]); Assert.Equal(0, result.Outputs["Signal"][1]);
        Assert.Equal(0, result.Outputs["Signal"][4]);
    }
    [Fact]
    public void ExtremePeriodsPreserveHalfPeriodClampAndLazySignalHistory()
    {
        var bars = Enumerable.Range(0, 12).Select(i => B(i + 2, i - 1, i)).ToArray();
        foreach (var kind in Kinds) { Check(bars, int.MaxValue, int.MaxValue, kind.Kind, kind.Reference); Check(bars, 1, 1, kind.Kind, kind.Reference); }
        Assert.Equal(2, EnhancedIndexWindow.MeanLength(1)); Assert.Equal(530, EnhancedIndexWindow.MeanLength(int.MaxValue));
        Assert.Equal(529, EnhancedIndexWindow.MeanLength(1057)); Assert.Equal(530, EnhancedIndexWindow.MeanLength(1059));
    }
    [Fact]
    public void CustomMeansKeepRequestOrderInputsAndSelectedCandles()
    {
        var bars = new[] { B(10, 0, 99), B(10, 0, 99), B(10, 0, 99) }; var selected = new[] { 2d, 4, 6 }; var line = new[] { .4, .8, 1.2 };
        foreach (var signal in new[] { false, true })
        {
            var requests = new List<int>(); var inputs = new List<double[]>();
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add(period); inputs.Add(values.ToArray()); return requests.Count == 1 ? new double[values.Count] : values.ToArray(); };
            using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, signal ? 2 : 1).ToArray());
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEnhancedIndexFast(data, context, 5, MovingAvgType.SimpleMovingAverage, 4, signal);
            Assert.Equal(line, fast.ToArray()); Assert.Equal(signal ? new[] { 3, 4 } : new[] { 3 }, requests); Assert.Equal(selected, inputs[0]); if (signal) Assert.Equal(line, inputs[1]); Assert.Equal(selected, data.ChainedValues);
        }
        var calls = 0; using var ignored = ComponentAverage.Arm((values, _) => { calls++; return values; }); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateEnhancedIndex(length: 5, signalLength: 4); Assert.Equal(0, calls);
        var expected = BuiltInFormulaReferences.EnhancedIndexValues(bars.Select((b, i) => B(b.High, b.Low, selected[i])).ToArray(), 5, 4, 1).Outputs;
        Assert.Equal(expected["Ei"], batch.CustomValuesList); Assert.Equal(expected["Signal"], batch.OutputValues["Signal"]);
    }
    [Fact]
    public void LegacyAverageStillUsesBothPublicComponents()
    {
        var bars = Enumerable.Range(0, 17).Select(i => B(i % 5 + 2, i % 5 - 2, i % 5 - 1)).ToArray(); var expected = Data(bars).CalculateEnhancedIndex(MovingAvgType.LinearWeightedMovingAverage, 5, 3);
        using var context = new ComputeContext(); foreach (var signal in new[] { false, true })
        { using var fast = IndicatorCompute.ComputeEnhancedIndexFast(Data(bars), context, 5, MovingAvgType.LinearWeightedMovingAverage, 3, signal); Assert.Equal(expected.OutputValues[signal ? "Signal" : "Ei"], fast.ToArray()); }
        Assert.Throws<NotSupportedException>(() => new EnhancedIndexState(MovingAvgType.LinearWeightedMovingAverage, 5, 3));
        var nativeExpected = Data(bars).CalculateEnhancedIndex(MovingAvgType.DoubleExponentialMovingAverage, 5, 3);
        using var state = new EnhancedIndexState(MovingAvgType.DoubleExponentialMovingAverage, 5, 3);
        for (var i = 0; i < bars.Length; i++) { var actual = state.Update(Native(bars[i]), true, true); Assert.Equal(nativeExpected.OutputValues["Ei"][i], actual.Value); Assert.Equal(nativeExpected.OutputValues["Signal"][i], actual.Outputs!["Signal"]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRangeOrMeanState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new EnhancedIndexState(length: 3, signalLength: 2); using var control = new EnhancedIndexState(length: 3, signalLength: 2); var seed = Native(B(4, 0, 3)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in new[] { B(5, 1, 2), B(7, -2, 1), B(3, -1, 0) }) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
