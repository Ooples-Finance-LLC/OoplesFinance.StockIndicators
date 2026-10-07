using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ZeroLagCycleNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ZLC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ZeroLagSmoothedCycle)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRegressionCascade(IndicatorValidationCase c, string route)
    {
        var options = (ZeroLagSmoothedCycleSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ZeroLagCycleValues(bars, options.Length).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(double[] prices, int length)
    {
        var bars = Bars(prices); var expected = BuiltInFormulaReferences.ZeroLagCycleValues(bars, length); var data = Data(bars).CalculateZeroLagSmoothedCycle(length); Assert.Equal(expected.Outputs.Keys, data.OutputValues.Keys); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Outputs["Filter"], data.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); using var fast = IndicatorCompute.ComputeZeroLagSmoothedCycleFast(Data(bars), context, length, key); Assert.Equal(expected.Outputs[key], fast.ToArray()); }
        using var state = new ZeroLagSmoothedCycleState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 1d, 3, 8, -2, 5, 7, -3 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false); foreach (var commit in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected.Outputs["Filter"][i], actual.Value); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key][i], actual.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    [Fact]
    public void RoundedStagesRetainWideAndSubnormalCancellation()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 14 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
        {
            Check(Enumerable.Range(0, 47).Select(i => (i % 7 - 3d) / 4 * scale).ToArray(), length);
            Check(Enumerable.Repeat(scale, 15).Concat(Enumerable.Repeat(0d, 25)).ToArray(), length);
        }
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepHistoriesLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, 2, 3, 1061, int.MaxValue }) { Check(Array.Empty<double>(), length); Check(new[] { 1d, 2, 4, -3, 8, 0, -5, 2 }, length); }
    }
    [Fact]
    public void LinearPricesAndSinglePeriodHaveNoCycle()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 100 }) foreach (var key in Check(Enumerable.Range(0, 35).Select(i => (double)i).ToArray(), length).Values) Assert.All(key, v => Assert.Equal(0, v));
    }
    [Fact]
    public void PowerOfTwoScalingPreservesBothOutputs()
    {
        var prices = Enumerable.Range(0, 43).Select(i => Math.Sin(i * .31)).ToArray(); var expected = Check(prices, 7);
        foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) { var actual = Check(prices.Select(p => p * scale).ToArray(), 7); foreach (var key in expected.Keys) Assert.Equal(expected[key].Select(p => p * scale), actual[key]); }
    }
    [Fact]
    public void SelectedPricesReachBothOutputsAndSignals()
    {
        var prices = Enumerable.Range(0, 43).Select(i => Math.Sin(i * .31)).ToArray(); var expected = BuiltInFormulaReferences.ZeroLagCycleValues(Bars(prices), 7); var bars = Bars(prices.Select(_ => 42d)); var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateZeroLagSmoothedCycle(7); Assert.Equal(expected.Signals, data.SignalsList);
        foreach (var key in expected.Outputs.Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeZeroLagSmoothedCycleFast(source, context, 7, key); Assert.Equal(expected.Outputs[key], fast.ToArray()); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAnyRegressionStage()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new ZeroLagSmoothedCycleState(3); using var control = new ZeroLagSmoothedCycleState(3); foreach (var bar in Bars(new[] { 1d, 3, 8, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
        }
    }
}
