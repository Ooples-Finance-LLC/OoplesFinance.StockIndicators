using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class GopalakrishnanNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = new(1e-10, 1e-12, true);
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(GopalakrishnanRangeIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeLogarithm(IndicatorValidationCase c, string route)
    {
        var options = (GopalakrishnanRangeIndexSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.GopalakrishnanOutputs(bars, options.Length, 2), Budget);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length = 5, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var k = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.GopalakrishnanOutputs(bars, length, k); var batch = Data(bars).CalculateGopalakrishnanRangeIndex(kind, length);
        using var context = new ComputeContext();
        foreach (var key in new[] { "Gapo", "Signal" })
        { Equal(expected[key], batch.OutputValues[key].ToArray()); using var raw = IndicatorCompute.ComputeGopalakrishnanRangeIndexFast(Data(bars), context, length, kind, key); Equal(expected[key], raw.ToArray()); }
        using var state = new GopalakrishnanRangeIndexState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue, double.MaxValue, -double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true); Assert.True(Budget.Accepts(expected["Gapo"][i], actual.Value));
                    foreach (var key in expected.Keys) Assert.True(Budget.Accepts(expected[key][i], actual.Outputs![key]), key + " at " + i);
                }
            }
        }
    }
    [Fact]
    public void HandRangeAndSignalRetainPartialWindowAndWeightedStartup()
    {
        var bars = new[] { Candle(0, 2), Candle(0, 4), Candle(0, 1), Candle(0, .5), Candle(0, 0) };
        var expected = BuiltInFormulaReferences.GopalakrishnanOutputs(bars, 2);
        Assert.Equal(new[] { 1d, 2, 2, 0, -1 }, expected["Gapo"]);
        Equal(new[] { 2d / 3, 5d / 3, 2, 2d / 3, -2d / 3 }, expected["Signal"]); Check(bars, 2);
        Check(Enumerable.Repeat(Candle(1, 1, 1), 8).ToArray());
    }
    [Fact]
    public void ExtendedRangesAndExpiryRecoverWithoutPublishedInfinity()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var length in new[] { 1, 2, 5 })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Enumerable.Range(0, 32).Select(i => Candle(0, scale, -scale)).ToArray(), length, kind);
            Check(Enumerable.Range(0, 32).Select(i => Candle(0, .5 + .4 * Math.Sin(i * .37), .1)).ToArray(), length, kind);
            Check(Array.Empty<Bar>(), length, kind);
        }
        var recovery = Enumerable.Range(0, 20).Select(i => i == 0 ? Candle(0, double.MaxValue, -double.MaxValue) : Candle(0, 1)).ToArray();
        var expected = BuiltInFormulaReferences.GopalakrishnanOutputs(recovery, 2); Assert.True(double.IsFinite(expected["Gapo"][0])); Assert.True(expected["Gapo"][0] > 1023); Assert.Equal(0, expected["Gapo"][^1]); Check(recovery, 2);
        using var context = new ComputeContext(); using var large = IndicatorCompute.ComputeGopalakrishnanRangeIndexFast(Data(recovery), context, int.MaxValue);
        Equal(BuiltInFormulaReferences.GopalakrishnanOutputs(recovery, int.MaxValue)["Gapo"], large.ToArray());
    }
    [Fact]
    public void SelectedPricesPreserveBothCandleRangeOutputs()
    {
        var bars = Enumerable.Range(0, 16).Select(i => Candle(0, 1 + i % 5, -.5)).ToArray(); var selected = bars.Select((b, i) => i % 2 == 0 ? double.MaxValue : -double.MaxValue).ToArray();
        var expected = BuiltInFormulaReferences.GopalakrishnanOutputs(bars, 2); using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.GopalakrishnanRangeIndex, new GopalakrishnanRangeIndexSpecOptions(2), key), context); Assert.NotNull(raw); Equal(expected[key], raw.Value.ToArray());
            var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Equal(expected[key], batch.CalculateGopalakrishnanRangeIndex(length: 2).OutputValues[key].ToArray());
        }
    }
    [Fact]
    public void TinyLogarithmsKeepTheirSignAndRejectErasure()
    {
        foreach (var range in new[] { Math.BitIncrement(1d), Math.BitDecrement(1d) })
        {
            var bars = Enumerable.Repeat(Candle(0, range), 8).ToArray(); var expected = BuiltInFormulaReferences.GopalakrishnanOutputs(bars, 2);
            Assert.All(expected["Gapo"], value => { Assert.NotEqual(0, value); Assert.Equal(Math.Sign(range - 1), Math.Sign(value)); });
            Check(bars, 2);
            var rules = BuiltInFormulaReferences.For(new GopalakrishnanRangeIndex(2)).ToArray();
            Assert.Throws<InvalidOperationException>(() => { foreach (var rule in rules) rule.Check(new IndicatorValidationContext("erased-logarithm", bars, new[] { new double[bars.Length], expected["Signal"] }, 0)); });
        }
    }
    [Fact]
    public void CustomerAveragesKeepPriceThenIndexOrder()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 7, 1 + i % 5)).ToArray(); var expected = BuiltInFormulaReferences.GopalakrishnanOutputs(bars, 2);
        foreach (var batch in new[] { false, true }) foreach (var key in expected.Keys)
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), input); return Enumerable.Repeat(99d, input.Count).ToArray(); },
                (input, period) => { Assert.Equal(2, period); Equal(expected["Gapo"], input.ToArray()); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var target = key == "Gapo" ? expected[key] : Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Equal(target, Data(bars).CalculateGopalakrishnanRangeIndex(length: 1).OutputValues[key].ToArray());
            else { using var raw = IndicatorCompute.ComputeGopalakrishnanRangeIndexFast(Data(bars), context, 1, outputKey: key); Equal(target, raw.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new GopalakrishnanRangeIndexState(); var control = new GopalakrishnanRangeIndexState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
