using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChoppinessNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = new(1e-9, 1e-12, true);
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChoppinessIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeLogarithm(IndicatorValidationCase c, string route)
    {
        var options = (ChoppinessIndexSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Ci", BuiltInFormulaReferences.ChoppinessOutputs(bars, options.Length) } }, Budget);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int length = 5)
    {
        var expected = BuiltInFormulaReferences.ChoppinessOutputs(bars, length); Equal(expected, Data(bars).CalculateChoppinessIndex(length: length).OutputValues["Ci"].ToArray());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeChoppinessIndexFast(Data(bars), context, length); Equal(expected, raw.ToArray());
        var core = new double[bars.Length]; OscillatorCore.ChoppinessIndex(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length); Equal(expected, core);
        using var state = new ChoppinessIndexState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue, double.MaxValue, -double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.True(Budget.Accepts(expected[i], actual.Value)); Assert.True(Budget.Accepts(expected[i], actual.Outputs!["Ci"])); }
            }
        }
    }
    [Fact]
    public void HandStartupUsesPartialSumAndFullLengthLogarithm()
    {
        var bars = Enumerable.Repeat(Candle(1, 2, 0), 8).ToArray(); var expected = BuiltInFormulaReferences.ChoppinessOutputs(bars, 2);
        Assert.Equal(0, expected[0]); Assert.All(expected.Skip(1), value => Assert.Equal(100, value)); Check(bars, 2);
        Check(Enumerable.Repeat(Candle(1, 1, 1), 8).ToArray());
        Check(new[] { Candle(3, 1, 0), Candle(1, 1, 0) }, 2);
    }
    [Fact]
    public void ExtremeRangesRatiosAndRollingTotalsRecover()
    {
        foreach (var length in new[] { 0, 1, 2, 5, 99, int.MaxValue })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Enumerable.Range(0, 32).Select(i => Candle((i % 3 - 1) * scale, scale, -scale)).ToArray(), length);
            Check(Enumerable.Range(0, 32).Select(i => Candle(.5 + .4 * Math.Sin(i * .37), .9, .1)).ToArray(), length);
            Check(Array.Empty<Bar>(), length);
        }
        var recovery = Enumerable.Range(0, 20).Select(i => i == 0 ? Candle(double.MaxValue, double.Epsilon, 0) : Candle(0, double.Epsilon, 0)).ToArray();
        var expected = BuiltInFormulaReferences.ChoppinessOutputs(recovery, 2); Assert.True(double.IsFinite(expected[0])); Assert.True(expected[0] > 100000); Assert.Equal(100, expected[^1]); Check(recovery, 2);
        Assert.Throws<ArgumentException>(() => OscillatorCore.ChoppinessIndex(new double[1], new double[1], new double[1], Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OscillatorCore.ChoppinessIndex(Array.Empty<double>(), new double[1], new double[1], new double[1]));
    }
    [Fact]
    public void RawSelectedClosesRetainOriginalExtrema()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 }; var original = selected.Select(_ => Candle(0)).ToArray();
        var expected = BuiltInFormulaReferences.ChoppinessOutputs(selected.Select(v => Candle(v)).ToArray(), 5); var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.ChoppinessIndex, new ChoppinessIndexSpecOptions(5)), context); Assert.NotNull(raw); Equal(expected, raw.Value.ToArray());
        var batch = Data(original); batch.SetCustomValues(selected.ToList()); Equal(expected, batch.CalculateChoppinessIndex(length: 5).OutputValues["Ci"].ToArray());
    }
    [Fact]
    public void CustomerSignalAverageReceivesSelectedPricesAndClampedPeriod()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 7)).ToArray(); var expected = BuiltInFormulaReferences.ChoppinessOutputs(bars, 2);
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (input, period) => { Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), input); return Enumerable.Repeat(99d, input.Count).ToArray(); } });
            using var context = new ComputeContext();
            if (batch) Equal(expected, Data(bars).CalculateChoppinessIndex(length: 1).OutputValues["Ci"].ToArray());
            else { using var raw = IndicatorCompute.ComputeChoppinessIndexFast(Data(bars), context, 1); Equal(expected, raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new ChoppinessIndexState(); var control = new ChoppinessIndexState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
