using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveFitNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ZLC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(AdaptiveLeastSquares)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptiveCenteredMoments(IndicatorValidationCase c, string route)
    {
        var options = (AdaptiveLeastSquaresSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AdaptiveFitValues(bars, options.Length).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length, double smooth = 1.5)
    {
        var expected = BuiltInFormulaReferences.AdaptiveFitValues(bars, length, smooth); var data = Data(bars).CalculateAdaptiveLeastSquares(length, smooth); Assert.Equal(new[] { "Als" }, data.OutputValues.Keys); Assert.Equal(expected.Outputs["Als"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeAdaptiveLeastSquaresFast(Data(bars), context, length, smooth); Assert.Equal(expected.Outputs["Als"], fast.ToArray());
        if (smooth == 1.5 && bars.All(b => b.High == b.Close && b.Low == b.Close)) { var output = new double[bars.Length]; OoplesFinance.StockIndicators.Core.MovingAverageCore.AdaptiveLeastSquares(bars.Select(b => b.Close).ToArray(), output, length); Assert.Equal(expected.Outputs["Als"], output); }
        using var state = new AdaptiveLeastSquaresState(length, smooth);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 1d, 3, 8, -2, 5, 7, -3 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false); foreach (var final in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Als"][i], actual.Value); Assert.Equal(actual.Value, actual.Outputs!["Als"]); } }
        }
        return expected.Outputs["Als"];
    }
    [Fact]
    public void CenteredMomentsRetainWideAndSubnormalCancellation()
    {
        foreach (var length in new[] { 1, 3, 7, 30 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        {
            Check(Bars(Enumerable.Range(0, 47).Select(i => (i % 7 - 3d) / 4 * scale)), length);
            Check(Bars(Enumerable.Repeat(scale, 12).Concat(Enumerable.Repeat(0d, 22))), length);
        }
    }
    [Fact]
    public void UnpublishedTrueRangesCanExceedDoubleMaximum()
    {
        var bars = Enumerable.Range(0, 45).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, i % 3 == 0 ? double.MaxValue : double.MaxValue / 4, -double.MaxValue, Math.Sin(i * .3) * double.MaxValue / 2, 1)).ToArray();
        foreach (var smooth in new[] { -2d, 0, .25, 1.5, 7 }) Check(bars, 5, smooth);
        var gaps = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, i % 2 == 0 ? -double.MaxValue / 4 : double.MaxValue, i % 2 == 0 ? -double.MaxValue : double.MaxValue / 4, (i % 2 == 0 ? -1d : 1d) * double.MaxValue / 2, 1)).ToArray(); Check(gaps, 5);
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepHistoriesLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 3, 500, int.MaxValue }) { Check(Array.Empty<Bar>(), length); Check(Bars(new[] { 1d, 2, 4, -3, 8, 0, -5, 2 }), length); }
    }
    [Fact]
    public void PowerOfTwoScalingPreservesRangeWeightsAndFit()
    {
        var prices = Enumerable.Range(0, 43).Select(i => Math.Sin(i * .31)).ToArray(); var expected = Check(Bars(prices), 7);
        foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) Assert.Equal(expected.Select(v => v * scale), Check(Bars(prices.Select(p => p * scale)), 7));
    }
    [Fact]
    public void SelectedPricesPreservePerBarRangePolicyAndSignals()
    {
        var prices = new[] { .5, 8, -.25, -9, 1d, 6, -1, 2, 9, 0, 4, -2 }; var original = prices.Select((_, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 2, -2, 1, 1)).ToArray();
        var effective = prices.Select((price, i) => new Bar(original[i].Time, 0, price >= -2 && price <= 2 ? 2 : Math.Max(price, i == 0 ? price : prices[i - 1]), price >= -2 && price <= 2 ? -2 : Math.Min(price, i == 0 ? price : prices[i - 1]), price, 1)).ToArray(); var expected = BuiltInFormulaReferences.AdaptiveFitValues(effective, 5);
        var data = Data(original); data.SetCustomValues(prices.ToList()); data.CalculateAdaptiveLeastSquares(5); Assert.Equal(expected.Outputs["Als"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        var source = Data(original); source.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeAdaptiveLeastSquaresFast(source, context, 5); Assert.Equal(expected.Outputs["Als"], fast.ToArray());
    }
    [Fact]
    public void InvalidSmoothingPreservesCallerValues()
    {
        foreach (var smooth in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateAdaptiveLeastSquares(smooth: smooth)); Assert.Equal(selected, data.CustomValuesList);
            Assert.Throws<ArgumentOutOfRangeException>(() => new AdaptiveLeastSquaresState(smooth: smooth)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeAdaptiveLeastSquaresFast(data, context, smooth: smooth)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRangesOrMoments()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new AdaptiveLeastSquaresState(3); using var control = new AdaptiveLeastSquaresState(3); foreach (var bar in Bars(new[] { 1d, 3, 8, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
