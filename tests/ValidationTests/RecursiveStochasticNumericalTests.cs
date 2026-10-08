using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RecursiveStochasticNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(RecursiveStochastic)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentTwoRangeFeedback(IndicatorValidationCase c, string route)
    {
        var options = (RecursiveStochasticSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Rsto", BuiltInFormulaReferences.RecursiveStochasticOutputs(bars, options.Length, options.Alpha) } }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length = 5, double alpha = .1)
    {
        var expected = BuiltInFormulaReferences.RecursiveStochasticOutputs(bars, length, alpha);
        Assert.Equal(expected, Data(bars).CalculateRecursiveStochastic(length, alpha).OutputValues["Rsto"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeRecursiveStochasticFast(Data(bars), context, length, alpha); Assert.Equal(expected, raw.ToArray());
        using var state = new RecursiveStochasticState(length, alpha);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue)), true, false); state.Update(Native(Candle(-double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Rsto"]); Assert.InRange(actual.Value, 0, 100); }
            }
        }
    }
    [Fact]
    public void HandFeedbackUsesThePreviousOutputAndASecondRange()
    {
        var bars = new[] { 1d, 2, 1, 3, 2, 4 }.Select(v => Candle(v)).ToArray();
        Assert.Equal(new[] { 0d, 100, 0, 0, 0, 100 }, BuiltInFormulaReferences.RecursiveStochasticOutputs(bars, 2, .5)); Check(bars, 2, .5);
        Check(Enumerable.Repeat(Candle(1), 9).ToArray());
    }
    [Fact]
    public void ExtremeRangesAndSubnormalPositionsRecover()
    {
        foreach (var length in new[] { 1, 2, 5, int.MaxValue }) foreach (var alpha in new[] { 0d, .1, .5, 1 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Enumerable.Range(0, 32).Select(i => Candle((i % 3 - 1) * scale)).ToArray(), length, alpha);
            Check(Enumerable.Range(0, 32).Select(i => Candle(.5 + .4 * Math.Sin(i * .37))).ToArray(), length, alpha); Check(Array.Empty<Bar>(), length, alpha);
        }
        Check(new[] { -double.MaxValue, double.MaxValue, 0, double.Epsilon, -double.Epsilon, 1d, 2, 1, 3, 2, 4 }.Select(v => Candle(v)).ToArray(), 2, .5);
    }
    [Fact]
    public void AlphaClampingMatchesPublicOptionsAndNaNIsRejected()
    {
        var bars = new[] { 1d, 3, 2, 5, 1, 4 }.Select(v => Candle(v)).ToArray();
        foreach (var alpha in new[] { -1d, 2, double.NegativeInfinity, double.PositiveInfinity }) Check(bars, 3, alpha);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecursiveStochasticState(alpha: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Data(bars).CalculateRecursiveStochastic(alpha: double.NaN));
        using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeRecursiveStochasticFast(Data(bars), context, alpha: double.NaN));
    }
    [Fact]
    public void SelectedClosesDriveBothRangeStages()
    {
        var selected = new[] { -double.MaxValue, double.MaxValue, 1, 4, 2, 5, 3, 1 }; var bars = selected.Select(_ => Candle(0)).ToArray();
        var expected = BuiltInFormulaReferences.RecursiveStochasticOutputs(selected.Select(v => Candle(v)).ToArray(), 3, .5);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.RecursiveStochastic, new RecursiveStochasticSpecOptions(3, .5)), context); Assert.NotNull(raw); Assert.Equal(expected, raw.Value.ToArray());
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateRecursiveStochastic(3, .5).OutputValues["Rsto"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new RecursiveStochasticState(); var control = new RecursiveStochasticState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
