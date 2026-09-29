using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveBandPassNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close) => new(DateTime.UnixEpoch, 0, 4, -4, close, 1);
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar{i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveBandPassFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptiveBandAndPeak(IndicatorValidationCase c, string route)
    {
        var options = (EhlersAdaptiveBandPassFilterSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AdaptiveBandPassValues(bars, options.Length1, options.Length2, options.Length3, options.Bw).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, int upper = 9, int lower = 3, int firstLag = 1, double width = .3)
    {
        var expected = BuiltInFormulaReferences.AdaptiveBandPassValues(bars, upper, lower, firstLag, width); var batch = Data(bars).CalculateEhlersAdaptiveBandPassFilter(upper, lower, firstLag, width); Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(expected.Outputs["Eabpf"], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], batch.OutputValues[key]); using var result = IndicatorCompute.ComputeEhlersAdaptiveBandPassFilterFast(Data(bars), context, upper, lower, firstLag, width, key); Equal(expected.Outputs[key], result.ToArray()); }
        using var state = new EhlersAdaptiveBandPassFilterState(upper, lower, firstLag, width);
        for (var replay = 0; replay < 2; replay++)
        {
            for (var i = 0; i < 22; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.True(IndicatorErrorBudget.Exact.Accepts(expected.Outputs["Eabpf"][i], point.Value)); foreach (var key in expected.Outputs.Keys) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected.Outputs[key][i], point.Outputs![key]), $"bar{i}/{key}: {expected.Outputs[key][i]:R} != {point.Outputs[key]:R}"); } }
        }
        Assert.All(expected.Outputs["Eabpf"], value => Assert.InRange(value, -1, 1)); Assert.All(expected.Outputs["Signal"], value => Assert.InRange(value, -.9, .9)); return expected.Outputs;
    }
    private static Bar[] Wave(double scale = 1, int count = 65) => Enumerable.Range(0, count).Select(i => Candle((Math.Sin(i * .37) + .13 * Math.Cos(i * .79)) * .8 * scale)).ToArray();
    [Fact]
    public void WideAndSubnormalPricesPreserveBandAndEnvelope()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) foreach (var width in new[] { 0d, .3, 1d }) Check(Wave(scale), width: width);
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsKeepLazyHistories()
    {
        Check(Array.Empty<Bar>()); Assert.All(Check(Enumerable.Repeat(Candle(0), 55).ToArray())["Eabpf"], value => Assert.Equal(0, value));
        foreach (var periods in new[] { (int.MinValue, 0, -1), (1, 1, 1), (2, 1, 0), (9, 3, 10), (9, 3, int.MaxValue), (int.MaxValue, 3, 1), (int.MaxValue, int.MaxValue, 1), (2, int.MaxValue, 1) }) Check(Wave(count: 35), periods.Item1, periods.Item2, periods.Item3);
    }
    [Fact]
    public void FiniteHugeWidthsStayPeriodicAndBounded()
    {
        // Lag above upper disables the spectrum; the clamped cycle is10 and .9*10 is exactly9.
        var expected = Check(Wave(), 10, 3, int.MaxValue, .25); var shifted = Check(Wave(), 10, 3, int.MaxValue, .25 + 9 * 1024); foreach (var key in expected.Keys) Assert.Equal(expected[key], shifted[key]);
        foreach (var width in new[] { 0d, 2.25, 4.5, 6.75, 9d, 1e100, double.MaxValue }) Check(Wave(), 10, 3, int.MaxValue, width);
    }
    [Fact]
    public void PowerOfTwoScalingPreservesNormalizedBand()
    {
        var expected = Check(Wave()); foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) { var actual = Check(Wave(scale)); foreach (var key in expected.Keys) Equal(expected[key], actual[key]); }
    }
    [Fact]
    public void QuietHistoryAndChangingCyclesPreserveCurrentEnvelope()
    {
        var bars = Enumerable.Range(0, 135).Select(i => Candle(i < 45 ? 0 : i < 90 ? Math.Sin(i * .37) * double.MaxValue : Math.Sin(i * .21) * Math.Pow(2, -1000))).ToArray(); var values = Check(bars)["Eabpf"]; Assert.All(values.Take(45), value => Assert.Equal(0, value)); Assert.Contains(values.Skip(45), value => Math.Abs(value) > .5);
    }
    [Fact]
    public void SelectedPricesReachBatchAndEveryFastOutput()
    {
        var selected = Wave().Select(b => b.Close).ToList(); var original = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.AdaptiveBandPassValues(Wave(), 9, 3, 1, .3); var data = Data(original); data.SetCustomValues(selected); data.CalculateEhlersAdaptiveBandPassFilter(9, 3, 1, .3); Assert.Equal(expected.Signals, data.SignalsList);
        foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(original); source.SetCustomValues(selected); using var context = new ComputeContext(); using var actual = IndicatorCompute.ComputeEhlersAdaptiveBandPassFilterFast(source, context, 9, 3, 1, .3, key); Equal(expected.Outputs[key], actual.ToArray()); }
    }
    [Fact]
    public void InvalidWidthsFailBeforeChangingCallerValues()
    {
        foreach (var width in new[] { -1d, -double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var source = Data(Wave(count: 4)); var selected = new List<double> { 7, 8, 9, 10 }; source.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => source.CalculateEhlersAdaptiveBandPassFilter(bw: width)); Assert.Equal(selected, source.CustomValuesList);
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveBandPassFilterState(bw: width)); Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersAdaptiveBandPassFilterSpecOptions(bw: width)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeEhlersAdaptiveBandPassFilterFast(source, context, bw: width)); Assert.Equal(selected, source.CustomValuesList);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceCycleBandOrPeak()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            using var state = new EhlersAdaptiveBandPassFilterState(7, 3, 1); using var control = new EhlersAdaptiveBandPassFilterState(7, 3, 1);
            foreach (var bar in Wave(count: 15)) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            foreach (var bar in Wave(count: 12)) { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
}
