using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PivotDetectorNumericalTests
{
    private static Bar[] Bars(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("PDO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 1;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(PivotDetectorOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentComponents(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.PivotDetectorValues(bars,
            kind: Kind(((PivotDetectorOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions()).MaType)).Outputs, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesFormula(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(double[] prices, int length1 = 3, int length2 = 2, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.PivotDetectorValues(bars, length1, length2, Kind(kind));
        var batch = Data(bars).CalculatePivotDetectorOscillator(kind, length1, length2);
        Assert.Equal(expected.Outputs["Pdo"], batch.OutputValues["Pdo"]); Assert.Equal(expected.Outputs["Pdo"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        Assert.All(expected.Outputs["Pdo"], value => Assert.InRange(value, -70, 160));
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePivotDetectorOscillatorFast(Data(bars), context, kind, length1, length2); Assert.Equal(expected.Outputs["Pdo"], fast.ToArray());
        using var state = new PivotDetectorOscillatorState(kind, length1, length2); using var window = new PivotDetectorWindow(kind, length1, length2);
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var b in Bars(new[] { 7d, -2, 1 })) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false); window.Next(-double.MaxValue, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(prices[i], final);
                    Assert.Equal(expected.Outputs["Pdo"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Pdo"]);
                    Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Trade);
                }
            }
        }
        return expected.Outputs["Pdo"];
    }
    [Fact]
    public void DirectSelectedPricesReachBatchAndFast()
    {
        var bars = Bars(new[] { 100d, 107, 94, 105, 90, 110 }); var selected = new[] { 3d, 2, 1, 4, 0, 5 };
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var expected = BuiltInFormulaReferences.PivotDetectorValues(Bars(selected), 3, 2, Kind(kind));
            var original = BuiltInFormulaReferences.PivotDetectorValues(bars, 3, 2, Kind(kind));
            Assert.False(expected.Outputs["Pdo"].SequenceEqual(original.Outputs["Pdo"]));
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
            using var fast = IndicatorCompute.ComputePivotDetectorOscillatorFast(data, context, kind, 3, 2);
            Assert.Equal(expected.Outputs["Pdo"], fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
            data.CalculatePivotDetectorOscillator(kind, 3, 2); Assert.Equal(expected.Outputs["Pdo"], data.OutputValues["Pdo"]);
            Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(bars.Select(b => b.Close), data.ClosePrices);
        }
    }
    [Fact]
    public void IndependentRsiRescalingAndExactBranchHands()
    {
        Assert.Equal(new[] { 130d, -40, -40 }, Check(new[] { 3d, 2, 1 }, 2, 2));
        Assert.Equal(new[] { 130d, 160, 160 }, Check(new[] { 1d, 1, 1 }, 2, 2));
        Assert.Equal(130, Check(new[] { 1d, Math.BitIncrement(1d), Math.BitIncrement(1d) }, 3, 2)[2]);
        Assert.Equal(60, Check(new[] { 0d, double.Epsilon, 0 }, 3, 2)[2]);
        Assert.Equal(0, Check(new[] { 100d, 107, 94 }, 200, 2)[2]);
    }
    [Fact]
    public void ExtremeAndSubnormalInputsPreserveUnpublishedComponents()
    {
        var pattern = new[] { 0d, 3, -1, 1, 2, -2, 0, 1, 4, -3, 2, 0 };
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var baseline = Check(pattern, 3, 2, kind);
            foreach (var scale in new[] { double.Epsilon, Math.Pow(2, 1020) }) Assert.Equal(baseline, Check(pattern.Select(v => v * scale).ToArray(), 3, 2, kind));
            Check(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, -double.MaxValue, double.MaxValue }, 3, 2, kind);
            Check(Enumerable.Repeat(double.MaxValue, 17).ToArray(), 3, 2, kind);
        }
    }
    [Fact]
    public void PeriodFloorsLazyHistoryAndPreviewReset()
    {
        var prices = new[] { 0d, 4, -1, 1, 7, -3, 0, 2, 1, -2 };
        foreach (var period in new[] { int.MinValue, 0, 1, 2, 5, int.MaxValue - 1, int.MaxValue })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<double>(), period, period, kind); Check(prices, period, period, kind); Check(prices, 2, period, kind); }
        Check(Enumerable.Range(0, 251).Select(i => (double)(i % 17 - 6)).ToArray(), 200, 14);
    }
    [Fact]
    public void CallbacksPreserveThreeSlotsPeriodsAndSelectedInput()
    {
        var bars = Bars(new[] { 1d, 7, 2, 5, 4 }); var selected = new[] { 3d, -1, 5, 2, 7 };
        var gains = new[] { 1d, 2, 7, 4, 5 }; var losses = new[] { 1d, 3, 13, 1, 2 }; var levels = new[] { 3d, 0, 0, 2, 10 };
        var expected = BuiltInFormulaReferences.PivotDetectorValues(bars, 3, 2, selected: selected, externalGain: gains, externalLoss: losses, externalLevel: levels);
        var requests = new List<(double[] Values, int Period)>(); var replacements = new[] { gains, losses, levels };
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { requests.Add((values.ToArray(), period)); return replacements[requests.Count - 1]; };
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputePivotDetectorOscillatorFast(data, context, MovingAvgType.SimpleMovingAverage, 3, 2);
        Assert.Equal(expected.Outputs["Pdo"], actual.ToArray()); Assert.Equal(new[] { 2, 2, 3 }, requests.Select(v => v.Period)); Assert.Equal(3, ComponentAverage.Substitutions);
        Assert.Equal(new[] { 0d, 0, 6, 0, 5 }, requests[0].Values); Assert.Equal(new[] { 0d, 4, 0, 3, 0 }, requests[1].Values); Assert.Equal(selected, requests[2].Values); Assert.Equal(selected, data.ChainedValues);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceComponents()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var badBars = Bars(new[] { 1d, bad }); Assert.Throws<ArgumentOutOfRangeException>(() => Data(badBars).CalculatePivotDetectorOscillator());
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputePivotDetectorOscillatorFast(Data(badBars), context));
            foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
            {
                using var state = new PivotDetectorOscillatorState(length1: 3, length2: 2); using var control = new PivotDetectorOscillatorState(length1: 3, length2: 2);
                foreach (var b in Bars(new[] { 0d, 3, -1 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
                var input = new[] { 1d, 3, -1, 1, 1 }; input[field] = bad;
                Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, input[0], input[1], input[2], input[3], input[4])), final, true));
                foreach (var b in Bars(new[] { 1d, -3, 0, 4 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
            }
        }
    }
    [Fact]
    public void UnsupportedNativeMeansRetainBatchFallback()
    {
        var bars = Bars(new[] { 2d, 5, 1, -2, 7, 3, 0 });
        var batch = Data(bars).CalculatePivotDetectorOscillator(MovingAvgType.LinearWeightedMovingAverage, 3, 2);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputePivotDetectorOscillatorFast(Data(bars), context, MovingAvgType.LinearWeightedMovingAverage, 3, 2);
        Assert.Equal(batch.CustomValuesList, fast.ToArray());
        Assert.Throws<NotSupportedException>(() => new PivotDetectorOscillatorState(MovingAvgType.LinearWeightedMovingAverage, 3, 2));
    }
}
