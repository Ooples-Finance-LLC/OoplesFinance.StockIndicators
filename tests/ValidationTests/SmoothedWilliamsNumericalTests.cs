using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SmoothedWilliamsNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SWR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SmoothedWilliamsR)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWilliamsRecurrence(IndicatorValidationCase c, string route)
    {
        var options = (SmoothedWilliamsRSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.SmoothedWilliamsOutputs(bars, options.Length, options.SmoothLength), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length, int smoothing)
    {
        var expected = BuiltInFormulaReferences.SmoothedWilliamsOutputs(bars, length, smoothing)["Swr"];
        Assert.Equal(expected, Data(bars).CalculateSmoothedWilliamsR(length, smoothing).OutputValues["Swr"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeSmoothedWilliamsRFast(Data(bars), context, length, smoothing); Assert.Equal(expected, raw.ToArray());
        var core = new double[bars.Length]; OscillatorCore.SmoothedWilliamsR(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length, smoothing); Assert.Equal(expected, core);
        using var state = new SmoothedWilliamsRState(length, smoothing);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(new Bar(DateTime.UnixEpoch, 0, double.MaxValue, -double.MaxValue, 0, 1)), true, false);
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false);
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
        }
    }
    [Fact]
    public void HandWarmupAndFlatWindowsUseMinusFifty()
    {
        var bars = new[] { 2d, 4, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v + 1, v - 1, v, 1)).ToArray();
        Assert.Equal(new[] { -50d, -50, -62.5 }, BuiltInFormulaReferences.SmoothedWilliamsOutputs(bars, 3, 3)["Swr"]); Check(bars, 3, 3);
        Assert.All(BuiltInFormulaReferences.SmoothedWilliamsOutputs(Bars(new[] { 7d, 7, 7, 7 }), 2, 3)["Swr"], v => Assert.Equal(-50d, v)); Check(Bars(new[] { 7d, 7, 7, 7 }), 2, 3);
    }
    [Fact]
    public void ExtremeRangesAndUnpublishedRecursionRecover()
    {
        foreach (var length in new[] { 1, 3, 14 }) foreach (var smoothing in new[] { 1, 3, 20 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) Check(Bars(new[] { scale, -scale, scale, 0, 0, -scale, scale, 0, 0, 0, 0, 0, 0, 0, 0, 0 }), length, smoothing);
            Check(Enumerable.Range(0, 30).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0, double.MaxValue, -double.MaxValue, i % 3 == 0 ? double.MaxValue : i % 3 == 1 ? -double.MaxValue : 0, 1)).ToArray(), length, smoothing);
            Check(Array.Empty<Bar>(), length, smoothing);
        }
        var recovery = Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0, 1, 0, i == 0 ? double.MaxValue : 0, 1)).ToArray();
        var expected = BuiltInFormulaReferences.SmoothedWilliamsOutputs(recovery, 1, 3)["Swr"];
        Assert.Equal(double.PositiveInfinity, expected[0]); Assert.True(double.IsFinite(expected[^1])); Check(recovery, 1, 3);
        var tinyRange = Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddDays(i), 0, double.Epsilon, 0, i == 0 ? 1d : 0, 1)).ToArray(); Check(tinyRange, 1, 1);
    }
    [Fact]
    public void MaximumPeriodsKeepPositiveGainAndObservedHistory()
    {
        var bars = new[] { 0d, 1, .25, .75, 0, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, 1, 0, v, 1)).ToArray();
        Check(bars, 1, int.MaxValue); Check(bars, int.MaxValue, 3); Check(bars, int.MaxValue, int.MaxValue);
    }
    [Fact]
    public void RawSelectedClosesRetainOriginalExtrema()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 };
        var original = selected.Select((_, i) => new Bar(DateTime.UnixEpoch.AddDays(i), 1, 10, -10, 0, 1)).ToArray();
        var projected = original.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray(); var expected = BuiltInFormulaReferences.SmoothedWilliamsOutputs(projected, 3, 3)["Swr"];
        var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeSmoothedWilliamsRFast(data, context, 3, 3); Assert.Equal(expected, raw.ToArray());
        Assert.Equal(expected, data.CalculateSmoothedWilliamsR(3, 3).OutputValues["Swr"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceExtremaOrSmoothing()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new SmoothedWilliamsRState(3, 3); using var control = new SmoothedWilliamsRState(3, 3);
            var seed = Native(Bars(new[] { 2d })[0]); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, 4, 3, 0, 8, 2, 9, 3, 0 })) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
