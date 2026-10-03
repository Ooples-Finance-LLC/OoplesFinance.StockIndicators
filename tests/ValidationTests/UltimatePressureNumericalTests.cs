using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UltimatePressureNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("UO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 2, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(UltimateOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWeightedPressure(IndicatorValidationCase c, string route)
    {
        var options = (UltimateOscillatorSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Uo", BuiltInFormulaReferences.UltimatePressureOutputs(bars, options.Length1, options.Length2, options.Length3) } }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int first, int second, int third)
    {
        var expected = BuiltInFormulaReferences.UltimatePressureOutputs(bars, first, second, third);
        Assert.Equal(expected, Data(bars).CalculateUltimateOscillator(first, second, third).OutputValues["Uo"]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeUltimateOscillatorFast(Data(bars), context, first, second, third); Assert.Equal(expected, raw.ToArray());
        var core = new double[bars.Length]; OscillatorCore.UltimateOscillator(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, first, second, third); Assert.Equal(expected, core);
        using var state = new UltimateOscillatorState(first, second, third);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(double.MaxValue, double.MaxValue, -double.MaxValue)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue, double.MaxValue, -double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Uo"]); }
            }
        }
    }
    [Fact]
    public void HandWeightsPartialWindowsAndZeroRangeRemainDistinct()
    {
        var bars = new[] { Candle(1), Candle(2), Candle(0) }; var expected = BuiltInFormulaReferences.UltimatePressureOutputs(bars, 1, 2, 3);
        foreach (var pair in expected.Zip(new[] { 50d, 625d / 7, 150d / 7 })) Assert.InRange(Math.Abs(pair.First - pair.Second), 0, 1e-12);
        Check(bars, 1, 2, 3); Assert.Equal(new[] { 50d, 100, 0 }, BuiltInFormulaReferences.UltimatePressureOutputs(bars, 1, 1, 1));
        Check(new[] { Candle(1, 1, 1), Candle(1, 1, 1) }, 1, 2, 3);
    }
    [Fact]
    public void ExtremeAndTinyRangesRemainBoundedAndRecover()
    {
        foreach (var periods in new[] { (1, 1, 1), (2, 3, 4), (7, 14, 28), (7, 2, 14) })
        {
            foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
                Check(Enumerable.Range(0, 72).Select(i => Candle((i % 3 - 1) * scale, scale, -scale)).ToArray(), periods.Item1, periods.Item2, periods.Item3);
            Check(Enumerable.Range(0, 72).Select(i => Candle(.5 + .4 * Math.Sin(i * .37), .9, .1)).ToArray(), periods.Item1, periods.Item2, periods.Item3);
            Check(new[] { Candle(0, double.Epsilon), Candle(double.MaxValue, double.Epsilon), Candle(0, double.Epsilon), Candle(-double.MaxValue, double.Epsilon), Candle(0, double.Epsilon) }, periods.Item1, periods.Item2, periods.Item3);
            Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, periods.Item3);
        }
        var recovery = Enumerable.Range(0, 40).Select(i => i < 2 ? Candle(i == 0 ? double.MaxValue : -double.MaxValue, double.MaxValue, -double.MaxValue) : Candle(1)).ToArray();
        var expected = BuiltInFormulaReferences.UltimatePressureOutputs(recovery, 2, 3, 4); Assert.Equal(50, expected[^1]); Assert.All(expected, v => Assert.InRange(v, 0, 100)); Check(recovery, 2, 3, 4);
    }
    [Fact]
    public void MaximumPeriodsUseObservedHistoryAndCoreChecksLengths()
    {
        Check(new[] { Candle(1), Candle(2), Candle(0) }, int.MaxValue, int.MaxValue, int.MaxValue);
        Check(new[] { Candle(1), Candle(2), Candle(0) }, 0, -1, 1);
        Assert.Throws<ArgumentException>(() => OscillatorCore.UltimateOscillator(new double[1], new double[1], new double[1], Array.Empty<double>()));
        Assert.Throws<ArgumentException>(() => OscillatorCore.UltimateOscillator(Array.Empty<double>(), new double[1], new double[1], new double[1]));
    }
    [Fact]
    public void RawSelectedClosesRetainOriginalRanges()
    {
        var selected = new[] { 1d, 3, -2, 5, -8, 3, 2, 7, 0, -4, 9, 2 }; var original = selected.Select(_ => Candle(0)).ToArray();
        var expected = BuiltInFormulaReferences.UltimatePressureOutputs(selected.Select(v => Candle(v)).ToArray(), 2, 3, 4);
        var data = Data(original); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.UltimateOscillator, new UltimateOscillatorSpecOptions(2, 3, 4)), context); Assert.NotNull(raw); Assert.Equal(expected, raw.Value.ToArray());
        var batch = Data(original); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected, batch.CalculateUltimateOscillator(2, 3, 4).OutputValues["Uo"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvancePressureOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new UltimateOscillatorState(2, 3, 4); using var control = new UltimateOscillatorState(2, 3, 4);
            var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
