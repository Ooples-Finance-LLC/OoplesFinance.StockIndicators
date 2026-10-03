using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ConvolutionNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select(p => new Bar(DateTime.UnixEpoch, p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersConvolutionIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesCenteredLagCorrelation(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ConvolutionOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Correlations) Check(Bar[] bars, int high = 3, int low = 4, int length = 5)
    {
        var expected = BuiltInFormulaReferences.ConvolutionValues(bars, high, low, length); var batch = Data(bars).CalculateEhlersConvolutionIndicator(high, low, length);
        Assert.Equal(expected.Outputs["Eci"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Eci", "Slope" })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersConvolutionFast(Data(bars), context, high, low, length, key == "Slope"); Assert.Equal(expected.Outputs[key], fast.ToArray()); }
        var state = new EhlersConvolutionIndicatorState(high, low, length); var window = new EhlersConvolutionWindow(high, low, length);
        for (var pass = 0; pass < 2; pass++)
        {
            if (pass != 0) { state.Reset(); window.Reset(); }
            for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
            {
                var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Eci"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Eci"]); Assert.Equal(expected.Outputs["Slope"][i], point.Outputs["Slope"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(point.Outputs["Slope"], direct.Slope); Assert.Equal(expected.Signals[i], direct.Trade); Assert.Equal(expected.Correlations[i], direct.Correlation);
            }
        }
        return expected;
    }
    [Fact]
    public void HandNeutralAndTwoPointCorrelationHaveKnownValues()
    {
        var neutral = Check(Bars(new double[8])); Assert.All(neutral.Outputs["Eci"], v => Assert.Equal(.25, v)); Assert.All(neutral.Outputs["Slope"], v => Assert.Equal(1, v)); Assert.All(neutral.Signals, v => Assert.Equal(Signal.None, v));
        Assert.All(Check(Bars(new[] { 0d, 1, -2, 7, 0 }), length: 1).Outputs["Eci"], v => Assert.Equal(.25, v));
        var negative = Check(Bars(new[] { 0d, 1, 0, 2, 0 }), 1, 1, 2); Assert.Equal(new[] { 0d, 0, -1, -1, -1 }, negative.Correlations);
        var exp = Math.Exp(-3); Assert.Equal(exp / (exp + 1) / 2, negative.Outputs["Eci"][2]);
        var positive = Check(Bars(new[] { 0d, 1, 4 }), 1, 1, 2); Assert.Equal(new[] { 0d, 0, 1 }, positive.Correlations); exp = Math.Exp(3); Assert.Equal(exp / (exp + 1) / 2, positive.Outputs["Eci"][2]);
    }
    [Fact]
    public void WideRoofingStagesRemainFiniteAfterNormalization()
    {
        foreach (var high in new[] { 1, 7, 80 }) foreach (var low in new[] { 1, 4, 40 })
        {
            var result = Check(Bars(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue }.Concat(Enumerable.Range(0, 20).Select(i => (double)(i % 7 - 3)))), high, low, 5);
            Assert.All(result.Outputs["Eci"], v => Assert.InRange(v, 0d, .5));
        }
        Check(Bars(Enumerable.Range(0, 25).Select(i => (i % 7 - 3) * double.Epsilon)), 3, 4, 5);
        Check(Bars(Enumerable.Range(0, 25).Select(i => i % 3 == 0 ? Math.BitIncrement(double.MaxValue / 2) : double.MaxValue / 2)), 7, 3, 4);
        Check(Bars(Enumerable.Repeat(double.MaxValue, 25)), 3, 4, 7);
    }
    [Fact]
    public void CorrelationIsInvariantUnderExactPowerOfTwoRescaling()
    {
        var prices = Enumerable.Range(0, 25).Select(i => (double)(i % 7 - 3)).ToArray(); var expected = Check(Bars(prices)).Outputs["Eci"];
        foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500), -1d }) Assert.Equal(expected, Check(Bars(prices.Select(p => p * scale))).Outputs["Eci"]);
    }
    [Fact]
    public void SlopeToleranceRetainsItsAbsoluteFloorAndExtendedScale()
    {
        var zero = ExactVarianceWindow.Units(0); var threshold = ExactVarianceWindow.Units(1e-12);
        Assert.Equal(1, EhlersConvolutionWindow.Slope(threshold, zero)); Assert.Equal(-1, EhlersConvolutionWindow.Slope(2 * threshold, zero)); Assert.Equal(1, EhlersConvolutionWindow.Slope(-2 * threshold, zero));
        var wide = ExactVarianceWindow.Units(double.MaxValue); Assert.Equal(-1, EhlersConvolutionWindow.Slope(2 * wide, wide)); Assert.Equal(1, EhlersConvolutionWindow.Slope(wide, 2 * wide)); Assert.Equal(1, EhlersConvolutionWindow.Slope(wide + 1, wide));
    }
    [Fact]
    public void ExtremePeriodsAllocateOnlyObservedCorrelationHistory()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue })
        {
            Check(Array.Empty<Bar>(), period, period, period); Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, 3, -2 }), period, period, period);
            Check(Bars(new[] { 1d, -1, 2, -3, 0 }), period, 4, 5); Check(Bars(new[] { 1d, -1, 2, -3, 0 }), 3, period, 5); Check(Bars(new[] { 1d, -1, 2, -3, 0 }), 3, 4, period);
        }
    }
    [Fact]
    public void CorrelationExpiryAndHistoryCompactionRetainThePredecessor()
    {
        var bars = Bars(Enumerable.Range(0, 2111).Select(i => (double)(i * 17 % 19 - 9))); var expected = BuiltInFormulaReferences.ConvolutionValues(bars, 3, 4, 3); var window = new EhlersConvolutionWindow(3, 4, 3);
        for (var i = 0; i < bars.Length; i++)
        { var preview = window.Next(bars[i].Close, false); var actual = window.Next(bars[i].Close, true); Assert.Equal(expected.Outputs["Eci"][i], actual.Value); Assert.Equal(expected.Outputs["Slope"][i], actual.Slope); Assert.Equal(expected.Signals[i], actual.Trade); Assert.Equal(actual, preview); }
    }
    [Fact]
    public void SelectedInputsAndNoMovingAverageCallbacksArePreserved()
    {
        var bars = Bars(new[] { 2d, -1, 4, 3, -5, 7, 0, 1 }); var selected = new[] { 5d, 1, -3, 7, 0, -2, 4, 8 }; var expected = BuiltInFormulaReferences.ConvolutionValues(Bars(selected), 3, 4, 5).Outputs; var calls = 0;
        using var armed = ComponentAverage.Arm((values, _) => { calls++; return values; }); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateEhlersConvolutionIndicator(3, 4, 5);
        foreach (var key in new[] { "Eci", "Slope" })
        { var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersConvolutionFast(data, context, 3, 4, 5, key == "Slope"); Assert.Equal(expected[key], batch.OutputValues[key]); Assert.Equal(expected[key], fast.ToArray()); Assert.Equal(selected, data.ChainedValues); }
        Assert.Equal(0, calls);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceFiltersOrCorrelationMoments()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            var state = new EhlersConvolutionIndicatorState(3, 4, 5); var control = new EhlersConvolutionIndicatorState(3, 4, 5); foreach (var b in Bars(new[] { 1d, 4, 0 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, -2, 1 })) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Slope"], actual.Outputs!["Slope"]); }
        }
    }
}
