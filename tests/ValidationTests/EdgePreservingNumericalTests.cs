using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EdgePreservingNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select(p => new Bar(DateTime.UnixEpoch, p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Ref)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EdgePreservingFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentSegments(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.EdgePreservingOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, bool[] Restarts) Check(Bar[] bars, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1, int length = 3, int smooth = 3)
    {
        var expected = BuiltInFormulaReferences.EdgePreservingValues(bars, reference, length, smooth);
        var batch = Data(bars).CalculateEdgePreservingFilter(kind, length, smooth); Assert.Equal(expected.Outputs["Epf"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Epf"], batch.OutputValues["Epf"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEdgePreservingFilterFast(Data(bars), context, length, kind, smooth); Assert.Equal(expected.Outputs["Epf"], fast.ToArray());
        using var state = new EdgePreservingFilterState(kind, length, smooth); using var window = new EdgePreservingWindow(kind, length, smooth);
        for (var pass = 0; pass < 2; pass++)
        {
            if (pass != 0) { state.Reset(); window.Reset(); }
            for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
            {
                var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Epf"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Epf"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Trade); Assert.Equal(expected.Restarts[i], direct.Restart);
            }
        }
        return expected;
    }
    [Fact]
    public void HandSegmentsKeepSeedAndOnlyRestartOnNewPeakEntries()
    {
        var seeded = Check(Bars(new[] { 3d, 6, 0 }), length: 1, smooth: 1);
        Assert.Equal(new[] { 3d, 4, 3 }, seeded.Outputs["Epf"]); Assert.All(seeded.Restarts, v => Assert.False(v));
        var reset = Check(Bars(new[] { 1d, 2, 3, 1, 4 }), smooth: 1);
        Assert.Equal(new[] { 1d, 1.5, 2, 1.75, 4 }, reset.Outputs["Epf"]); Assert.Equal(new[] { true, false, false, false, true }, reset.Restarts);
        Check(Bars(new[] { -1d, -2, -3, -1, -4 }), smooth: 1);
        Assert.Equal(new[] { double.MaxValue, double.MaxValue, double.MaxValue / 2, double.MaxValue / 5 }, Check(Bars(new[] { double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue }), length: 1, smooth: 1).Outputs["Epf"]);
    }
    [Fact]
    public void WideRoundedOffsetsAndRegressionRecoverAcrossAllMeans()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -540), 1d, double.MaxValue / 16 })
            Check(Bars(Enumerable.Range(0, 21).Select(i => (i % 7 - 3) * scale)), kind.Kind, kind.Ref, 4, 5);
        foreach (var kind in Kinds)
        {
            var result = Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue }.Concat(Enumerable.Range(0, 30).Select(i => (double)(i % 5 - 2)))), kind.Kind, kind.Ref, 3, 4);
            Assert.All(result.Outputs["Epf"], value => Assert.True(double.IsFinite(value)));
        }
        Check(Bars(Enumerable.Repeat(double.MaxValue, 60)), length: 7, smooth: 5);
        Check(Bars(Enumerable.Range(0, 27).Select(i => i % 3 == 0 ? Math.BitIncrement(1) : Math.BitDecrement(1))), length: 3, smooth: 7);
    }
    [Fact]
    public void PeakToleranceUsesRoundedRatioAndRejectsZeroPeaks()
    {
        var unit = ExactVarianceWindow.Units(1); Assert.False(EdgePreservingWindow.NearPeak(unit, 0));
        Assert.True(EdgePreservingWindow.NearPeak(unit, unit)); Assert.True(EdgePreservingWindow.NearPeak(ExactVarianceWindow.Units(1 - .5e-12), unit)); Assert.False(EdgePreservingWindow.NearPeak(ExactVarianceWindow.Units(1 - 2e-12), unit));
        Assert.True(EdgePreservingWindow.NearPeak(-unit, -unit)); Assert.False(EdgePreservingWindow.NearPeak(-2 * unit, -unit));
        var wide = ExactVarianceWindow.Units(double.MaxValue) * 2; Assert.True(EdgePreservingWindow.NearPeak(wide, wide)); Assert.False(EdgePreservingWindow.NearPeak(wide, unit));
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedPricesAndOffsets()
    {
        foreach (var kind in Kinds) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<Bar>(), kind.Kind, kind.Ref, period, period); Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, -2, 3 }), kind.Kind, kind.Ref, period, period); }
    }
    [Fact]
    public void CoreUsesPublicSegmentsAndSupportsInPlace()
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -2, 3, -1, 0, 7, 4 }; var bars = Bars(prices);
        foreach (var length in new[] { 0, 1, 4, int.MaxValue })
        {
            var expected = BuiltInFormulaReferences.EdgePreservingValues(bars, 1, length, 50).Outputs["Epf"]; var output = Enumerable.Repeat(97d, prices.Length + 1).ToArray(); MovingAverageCore.EdgePreservingFilter(prices, output, length); Assert.Equal(expected, output.Take(prices.Length)); Assert.Equal(97, output[^1]);
            var inplace = prices.ToArray(); MovingAverageCore.EdgePreservingFilter(inplace, inplace, length); Assert.Equal(expected, inplace);
        }
        Assert.Throws<ArgumentException>(() => MovingAverageCore.EdgePreservingFilter(prices, new double[prices.Length - 1])); MovingAverageCore.EdgePreservingFilter(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
    }
    [Fact]
    public void SelectedInputsAndLegacyAverageKindsAgree()
    {
        var bars = Bars(new[] { 2d, -1, 4, 3, -5, 7, 0, 1 }); Check(bars, MovingAvgType.DoubleExponentialMovingAverage, 4, 3, 4);
        var selected = new[] { 5d, 1, -3, 7, 0, -2, 4, 8 }; var data = Data(bars); data.SetCustomValues(selected.ToList()); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateEdgePreservingFilter(length: 3, smoothLength: 4);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEdgePreservingFilterFast(data, context, 3, smoothLength: 4); Assert.Equal(batch.CustomValuesList, fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
    }
    [Fact]
    public void CustomMeanConsumesOneFastSlotAndNoBatchSlots()
    {
        var bars = Bars(new[] { 1d, 4, 2, -1, 7, 3, 5, 0 }); var means = new[] { 0d, 3, 0, 1, -2, 3, 1, -1 }; var expected = BuiltInFormulaReferences.EdgePreservingValues(bars, 1, 3, 4, means).Outputs["Epf"];
        var calls = 0;
        using (ComponentAverage.Arm((values, period) => { calls++; Assert.Equal(3, period); Assert.Equal(bars.Select(b => b.Close), values); return means; }))
        { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeEdgePreservingFilterFast(Data(bars), context, 3, smoothLength: 4); Assert.Equal(expected, output.ToArray()); }
        Assert.Equal(1, calls); calls = 0;
        using (ComponentAverage.Arm((values, _) => { calls++; return means; }))
        { var batch = Data(bars).CalculateEdgePreservingFilter(length: 3, smoothLength: 4); Assert.Equal(BuiltInFormulaReferences.EdgePreservingValues(bars, 1, 3, 4).Outputs["Epf"], batch.CustomValuesList); }
        Assert.Equal(0, calls);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAnyState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new EdgePreservingFilterState(length: 3, smoothLength: 4); using var control = new EdgePreservingFilterState(length: 3, smoothLength: 4);
            foreach (var b in Bars(new[] { 1d, 4, 0 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, -2, 1 })) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Epf"], actual.Outputs!["Epf"]); }
        }
    }
}
