using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EarlyOnsetNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersEarlyOnsetTrendIndicator)).Select(c => new object[] { c });
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
    { var o = (EhlersEarlyOnsetTrendIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Eoti", BuiltInFormulaReferences.EarlyOnsetValues(bars, o.Length1, o.Length2, o.K) } }; }
    private static void Check(Bar[] bars, int low = 30, int high = 100, double k = .85)
    {
        var expected = BuiltInFormulaReferences.EarlyOnsetValues(bars, low, high, k); var batch = Data(bars).CalculateEhlersEarlyOnsetTrendIndicator(low, high, k);
        Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Eoti"]);
        var spec = new IndicatorSpec(IndicatorName.EhlersEarlyOnsetTrendIndicator, new EhlersEarlyOnsetTrendIndicatorSpecOptions(low, high, k), "Eoti");
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(fast); Assert.Equal(expected, fast.Value.ToArray());
        var core = new double[bars.Length]; OscillatorCore.EhlersEarlyOnsetTrendIndicator(bars.Select(b => b.Close).ToArray(), core, low, high, k); Assert.Equal(expected, core);
        var state = StatefulIndicatorFactory.Create(spec); Assert.IsType<EhlersEarlyOnsetTrendIndicatorState>(state);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Eoti"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyValuesPreserveFilterPeakAndQuotient()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var k in new[] { -.85, 0, .85, 2d, double.MaxValue })
            Check(Enumerable.Range(0, 45).Select(i => Candle(i < 25 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), 30, 100, k);
    }
    [Fact]
    public void PowerOfTwoScalingPreservesTheEntireQuotientTrajectory()
    {
        var bars = Enumerable.Range(0, 65).Select(i => Candle(i < 35 ? i % 5 - 2 : 0)).ToArray(); var expected = Data(bars).CalculateEhlersEarlyOnsetTrendIndicator().CustomValuesList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var scaled = bars.Select(b => Candle(b.Close * scale)).ToArray(); Assert.Equal(expected, Data(scaled).CalculateEhlersEarlyOnsetTrendIndicator().CustomValuesList); Check(scaled); }
    }
    [Fact]
    public void FirstImpulseZeroHighPassAndQuotientPolesRetainTheirContracts()
    {
        foreach (var k in new[] { -2d, -.85, 0d, .85, 1d, 2d, double.MaxValue })
        {
            var positive = new[] { Candle(double.Epsilon) }; Assert.Equal(1d, Data(positive).CalculateEhlersEarlyOnsetTrendIndicator(k: k).CustomValuesList[0]); Check(positive, k: k);
            var zero = Enumerable.Repeat(Candle(0), 8).ToArray(); Assert.All(Data(zero).CalculateEhlersEarlyOnsetTrendIndicator(k: k).CustomValuesList, v => Assert.Equal(k, v)); Check(zero, k: k);
            var prices = new[] { Candle(1), Candle(-1), Candle(double.MaxValue) }; Assert.All(Data(prices).CalculateEhlersEarlyOnsetTrendIndicator(length2: 1, k: k).CustomValuesList, v => Assert.Equal(k, v)); Check(prices, high: 1, k: k);
        }
        foreach (var pair in new[] { (1d, -1d), (-1d, 1d) })
        { var prices = new[] { Candle(pair.Item1) }; Assert.Equal(0d, Data(prices).CalculateEhlersEarlyOnsetTrendIndicator(k: pair.Item2).CustomValuesList[0]); Check(prices, k: pair.Item2); }
    }
    [Fact]
    public void BothPeriodsNormalizeIndependentlyWithoutHistoryAllocation()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 3)).ToArray();
        foreach (var pair in new[] { (int.MinValue, 0), (0, int.MaxValue), (int.MaxValue, -1), (int.MaxValue, int.MaxValue), (30, 1), (1, 100) })
        { Check(bars, pair.Item1, pair.Item2); Check(Array.Empty<Bar>(), pair.Item1, pair.Item2); }
    }
    [Fact]
    public void SelectedPricesReachBatchCoreAndTypedArm()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.EarlyOnsetValues(selected.Select(v => Candle(v)).ToArray(), 20, 40, .5); var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersEarlyOnsetTrendIndicator(20, 40, .5); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected); using var arm = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersEarlyOnsetTrendIndicator, new EhlersEarlyOnsetTrendIndicatorSpecOptions(20, 40, .5), "Eoti"), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
    }
    [Fact]
    public void InvalidKIsRejectedEvenForEmptyInputs()
    {
        foreach (var k in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersEarlyOnsetTrendIndicatorState(k: k));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersEarlyOnsetTrendIndicator(k: k));
            Assert.Throws<ArgumentOutOfRangeException>(() => OscillatorCore.EhlersEarlyOnsetTrendIndicator(Array.Empty<double>(), Array.Empty<double>(), k: k));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeEhlersEarlyOnsetTrendIndicatorFast(Data(Array.Empty<Bar>()), context, k: k));
        }
    }
    [Fact]
    public void InvalidFieldsLeaveHighPassLowPassAndPeakUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersEarlyOnsetTrendIndicatorState(); var control = new EhlersEarlyOnsetTrendIndicatorState(); var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
