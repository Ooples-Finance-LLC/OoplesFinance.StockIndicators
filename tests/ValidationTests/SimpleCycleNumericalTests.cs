using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SimpleCycleNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SimpleCycle)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Sc", BuiltInFormulaReferences.SimpleCycleOutputs(bars, ((SimpleCycleSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions()).Length) } }, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length = 50)
    {
        var expected = BuiltInFormulaReferences.SimpleCycleOutputs(bars, length); var batch = Data(bars).CalculateSimpleCycle(length); Assert.Equal(expected, batch.OutputValues["Sc"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeSimpleCycleFast(Data(bars), context, length); Assert.Equal(expected, raw.ToArray());
        using var state = new SimpleCycleState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Sc"]); }
            }
        }
    }
    [Fact]
    public void HandFeedbackAndLaggedAugmentedSource()
    {
        var bars = new[] { 1d, 2, 4, 8, 16 }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { 1d, 2, 3, 5, 10 }, BuiltInFormulaReferences.SimpleCycleOutputs(bars, 1)); Check(bars, 1); Check(bars, 0); Check(bars);
        Assert.Equal(.02, BuiltInFormulaReferences.SimpleCycleOutputs(bars)[0]);
        Check(bars, 2000); Check(bars, int.MaxValue); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void OversizedSourcesCancelWithoutPoisoningRecurrence()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 100).Select(i => Candle(i < 80 ? (i % 5 - 2) / 2d * scale : 1)).ToArray(); Check(bars); Check(bars, 1); Check(bars, 3);
        }
        var repeated = Enumerable.Repeat(Candle(double.MaxValue), 12).Concat(Enumerable.Repeat(Candle(0), 30)).ToArray(); var expected = BuiltInFormulaReferences.SimpleCycleOutputs(repeated, 1);
        Assert.Equal(double.MaxValue, expected[0]); Assert.Equal(double.MaxValue, expected[1]); Assert.Equal(0, expected[2]); Assert.All(expected, v => Assert.True(double.IsFinite(v))); Check(repeated, 1); Check(repeated, 2);
    }
    [Fact]
    public void SelectedSourceDrivesRawDispatcherAndLegacy()
    {
        var selected = Enumerable.Range(0, 80).Select(i => i % 5 == 0 ? double.MaxValue : i % 5 == 1 ? -double.MaxValue : 1 + i % 7).ToArray(); var bars = selected.Select(_ => Candle(0)).ToArray(); var expected = BuiltInFormulaReferences.SimpleCycleOutputs(selected.Select(v => Candle(v)).ToArray()); Assert.Contains(expected, v => v != 0);
        using var context = new ComputeContext(); var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeSimpleCycleFast(direct, context); Assert.Equal(expected, raw.ToArray());
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.SimpleCycle, new SimpleCycleSpecOptions(50)), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateSimpleCycle().OutputValues["Sc"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new SimpleCycleState(); using var control = new SimpleCycleState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
