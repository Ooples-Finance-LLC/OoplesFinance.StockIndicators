using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class BelkhayateNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BelkhayateTiming)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentFiveBarNormalization(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Belkhayate", BuiltInFormulaReferences.BelkhayateOutputs(bars) } }, IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars)
    {
        var expected = BuiltInFormulaReferences.BelkhayateOutputs(bars); Assert.Equal(expected, Data(bars).CalculateBelkhayateTiming().OutputValues["Belkhayate"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeBelkhayateTimingFast(Data(bars), context); Assert.Equal(expected, raw.ToArray());
        var core = new double[bars.Length]; OscillatorCore.BelkhayateTiming(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), core); Assert.Equal(expected, core);
        var state = new BelkhayateTimingState();
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue, double.MaxValue, -double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Belkhayate"]); }
            }
        }
    }
    [Fact]
    public void HandStartupRetainsFiveBarZeroPadding()
    {
        var bars = Enumerable.Repeat(Candle(3), 5).ToArray(); var expected = BuiltInFormulaReferences.BelkhayateOutputs(bars);
        foreach (var pair in expected.Zip(new[] { 16.25, 6.875, 3.75, 2.1875, 1.25 })) Assert.InRange(Math.Abs(pair.First - pair.Second), 0, 1e-12);
        Check(bars); Check(Enumerable.Repeat(Candle(1, 1, 1), 8).ToArray());
    }
    [Fact]
    public void ExtremeMediansRangesAndPublishedValuesRecover()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            Check(Enumerable.Range(0, 64).Select(i => Candle((i % 3 - 1) * scale, scale, -scale)).ToArray());
            Check(Enumerable.Range(0, 64).Select(i => Candle(scale, scale, i % 2 == 0 ? scale : 0)).ToArray());
        }
        Check(Enumerable.Range(0, 64).Select(i => Candle(.5 + .4 * Math.Sin(i * .37), .9, .1)).ToArray()); Check(Array.Empty<Bar>());
        var recovery = Enumerable.Range(0, 20).Select(i => Candle(i == 0 ? double.MaxValue : .5, 1)).ToArray();
        var expected = BuiltInFormulaReferences.BelkhayateOutputs(recovery); Assert.True(double.IsPositiveInfinity(expected[0])); Assert.Equal(0, expected[^1]); Check(recovery);
    }
    [Fact]
    public void CompatibilityLengthLeavesTheFixedWindowUnchanged()
    {
        var bars = Enumerable.Range(0, 16).Select(i => Candle(i % 7)).ToArray(); var expected = BuiltInFormulaReferences.BelkhayateOutputs(bars);
        foreach (var length in new[] { 0, 1, 2, 5, 99, int.MaxValue })
        { using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeBelkhayateTimingFast(Data(bars), context, length); Assert.Equal(expected, raw.ToArray()); }
        Assert.Throws<ArgumentException>(() => OscillatorCore.BelkhayateTiming(new double[1], new double[1], new double[1], Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OscillatorCore.BelkhayateTiming(new double[1], Array.Empty<double>(), new double[1], new double[1]));
    }
    [Fact]
    public void RawSelectedClosesRetainOriginalExtrema()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 }; var original = selected.Select(_ => Candle(0)).ToArray();
        var expected = BuiltInFormulaReferences.BelkhayateOutputs(selected.Select(v => Candle(v)).ToArray()); var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.BelkhayateTiming, new BelkhayateTimingSpecOptions(5)), context); Assert.NotNull(raw); Assert.Equal(expected, raw.Value.ToArray());
        var batch = Data(original); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateBelkhayateTiming().OutputValues["Belkhayate"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new BelkhayateTimingState(); var control = new BelkhayateTimingState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
