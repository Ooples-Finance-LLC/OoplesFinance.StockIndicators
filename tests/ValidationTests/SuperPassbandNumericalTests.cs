using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SuperPassbandNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSuperPassbandFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = (EhlersSuperPassbandFilterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.SuperPassbandOutputs(bars, o.FastLength, o.SlowLength, o.Length1, o.Length2); }
    private static void Check(Bar[] bars, int fast = 40, int slow = 60, int numerator = 5, int rms = 50)
    {
        var expected = BuiltInFormulaReferences.SuperPassbandOutputs(bars, fast, slow, numerator, rms); var batch = Data(bars).CalculateEhlersSuperPassbandFilter(fast, slow, numerator, rms);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]); Assert.Equal(expected["Espf"], batch.CustomValuesList);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            var spec = new IndicatorSpec(IndicatorName.EhlersSuperPassbandFilter, new EhlersSuperPassbandFilterSpecOptions(fast, slow, numerator, rms), key);
            using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray());
        }
        var core = new double[bars.Length]; OscillatorCore.EhlersSuperPassbandFilter(bars.Select(b => b.Close).ToArray(), core, fast, slow, numerator, rms); Assert.Equal(expected["Espf"], core);
        using var state = new EhlersSuperPassbandFilterState(fast, slow, numerator, rms);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var p = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Espf"][i], p.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], p.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void WideAndSubnormalInputsPreserveFilterAndExactRms()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var periods in new[] { (2, 4, 1), (40, 60, 5), (60, 40, 5) })
            Check(Enumerable.Range(0, 40).Select(i => Candle(i < 25 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), periods.Item1, periods.Item2, periods.Item3, 7);
    }
    [Fact]
    public void EqualGainsCancelExactlyAndConstantPricesRetainOnlyTransient()
    {
        var prices = Enumerable.Range(0, 80).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue)).ToArray();
        foreach (var periods in new[] { (20, 20, 1), (1, 2, int.MaxValue), (int.MaxValue, int.MaxValue - 1, 1) })
        {
            Check(prices, periods.Item1, periods.Item2, periods.Item3, 5);
            var actual = Data(prices).CalculateEhlersSuperPassbandFilter(periods.Item1, periods.Item2, periods.Item3, 5);
            foreach (var values in actual.OutputValues.Values) Assert.All(values, v => Assert.Equal(0d, v));
        }
        Check(Enumerable.Repeat(Candle(double.MaxValue), 80).ToArray(), 2, 4, 1, 5);
    }
    [Fact]
    public void HandImpulseAndPartialRmsStartupMatchTransferFunction()
    {
        var bars = new[] { Candle(1), Candle(0), Candle(0) }; var output = Data(bars).CalculateEhlersSuperPassbandFilter(2, 4, 1, 2).OutputValues;
        Assert.Equal(new[] { .25, .0625, -.015625 }, output["Espf"]);
        Assert.Equal(.25, output["UpperBand"][0]); Assert.Equal(Math.Sqrt((.25 * .25 + .0625 * .0625) / 2), output["UpperBand"][1]);
        Assert.Equal(Math.Sqrt((.0625 * .0625 + .015625 * .015625) / 2), output["UpperBand"][2]);
        Assert.Equal(output["UpperBand"].Select(v => -v), output["LowerBand"]);
        Check(bars, 2, 4, 1, 2);
    }
    [Fact]
    public void EveryPeriodNormalizesIndependentlyAndHistoryGrowsAsNeeded()
    {
        var bars = Enumerable.Range(0, 8).Select(i => Candle(i % 3)).ToArray();
        foreach (var p in new[] { (int.MinValue, 4, 1, 5), (2, 0, 1, 5), (2, 4, 0, 5), (2, 4, 1, int.MinValue), (2, 4, 1, int.MaxValue), (int.MaxValue, 4, 1, 5), (2, int.MaxValue, 1, 5), (2, 4, int.MaxValue, 5) })
        { Check(bars, p.Item1, p.Item2, p.Item3, p.Item4); Check(Array.Empty<Bar>(), p.Item1, p.Item2, p.Item3, p.Item4); }
    }
    [Fact]
    public void SelectedPricesReachEveryOutputAndTypedArm()
    {
        var selected = Enumerable.Range(0, 24).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.SuperPassbandOutputs(selected.Select(v => Candle(v)).ToArray(), 2, 4, 1, 5);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersSuperPassbandFilter(2, 4, 1, 5);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]); var data = Data(bars); data.SetCustomValues(selected);
            using var arm = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersSuperPassbandFilter, new EhlersSuperPassbandFilterSpecOptions(2, 4, 1, 5), key), context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray());
        }
    }
    [Fact]
    public void InvalidFieldsLeaveBothPolesAndRollingSquaresUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersSuperPassbandFilterState(2, 4, 1, 5); using var control = new EhlersSuperPassbandFilterState(2, 4, 1, 5);
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false); var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3))))
            { var a = control.Update(bar, true, true); var b = state.Update(bar, true, true); Assert.Equal(a.Value, b.Value); foreach (var key in a.Outputs!.Keys) Assert.Equal(a.Outputs[key], b.Outputs![key]); }
        }
    }
}
