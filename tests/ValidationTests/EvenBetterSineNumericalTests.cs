using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EvenBetterSineNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersEvenBetterSineWaveIndicator)).Select(c => new object[] { c });
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
    { var o = (EhlersEvenBetterSineWaveIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Ebsi", BuiltInFormulaReferences.EvenBetterSineValues(bars, o.Length1, o.Length2) } }; }
    private static void Check(Bar[] bars, int high = 40, int low = 10)
    {
        var expected = BuiltInFormulaReferences.EvenBetterSineValues(bars, high, low); var batch = Data(bars).CalculateEhlersEvenBetterSineWaveIndicator(high, low);
        Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Ebsi"]); Assert.All(expected, v => Assert.InRange(v, -1d, 1d));
        var spec = new IndicatorSpec(IndicatorName.EhlersEvenBetterSineWaveIndicator, new EhlersEvenBetterSineWaveIndicatorSpecOptions(high, low), "Ebsi");
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(fast); Assert.Equal(expected, fast.Value.ToArray());
        var core = new double[bars.Length]; OscillatorCore.EhlersEvenBetterSineWaveIndicator(bars.Select(b => b.Close).ToArray(), core, high, low); Assert.Equal(expected, core);
        var state = StatefulIndicatorFactory.Create(spec); Assert.IsType<EhlersEvenBetterSineWaveIndicatorState>(state);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Ebsi"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyValuesRetainFilterAndThreeSampleNormalization()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var periods in new[] { (1, 1), (40, 10), (10, 40) })
            Check(Enumerable.Range(0, 40).Select(i => Candle(i < 25 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), periods.Item1, periods.Item2);
    }
    [Fact]
    public void FirstMotionUsesThreeSamplesAndConstantInputStaysZero()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = new[] { Candle(0), Candle(scale), Candle(0), Candle(0) }; var line = Data(bars).CalculateEhlersEvenBetterSineWaveIndicator().CustomValuesList;
            Assert.Equal(0d, line[0]); Assert.InRange(Math.Abs(line[1] - 1 / Math.Sqrt(3)), 0, 2e-16); Check(bars);
            var constant = Enumerable.Repeat(Candle(scale), 10).ToArray(); Assert.All(Data(constant).CalculateEhlersEvenBetterSineWaveIndicator().CustomValuesList, v => Assert.Equal(0d, v)); Check(constant);
        }
    }
    [Fact]
    public void AlternatingTailAndPowerOfTwoScalesPreserveTheTrajectory()
    {
        var bars = Enumerable.Range(0, 300).Select(i => Candle(i % 2 == 0 ? -1 : 1)).ToArray(); var expected = Data(bars).CalculateEhlersEvenBetterSineWaveIndicator(20, 5).CustomValuesList;
        var pole = Math.Cos(2 * Math.PI / 20) / (1 + Math.Sin(2 * Math.PI / 20)); var limit = (1 + pole + pole * pole) / Math.Sqrt(3 * (1 + pole * pole + Math.Pow(pole, 4)));
        Assert.InRange(Math.Abs(expected[299] - limit), 0, 2e-14);
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var scaled = bars.Select(b => Candle(b.Close * scale)).ToArray(); Assert.Equal(expected, Data(scaled).CalculateEhlersEvenBetterSineWaveIndicator(20, 5).CustomValuesList); Check(scaled, 20, 5); }
    }
    [Fact]
    public void BothPeriodsNormalizeIndependentlyWithoutHistoryAllocation()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 3)).ToArray();
        foreach (var pair in new[] { (int.MinValue, 0), (0, int.MaxValue), (int.MaxValue, -1), (int.MaxValue, int.MaxValue), (40, 1), (1, 10) })
        { Check(bars, pair.Item1, pair.Item2); Check(Array.Empty<Bar>(), pair.Item1, pair.Item2); }
    }
    [Fact]
    public void SelectedPricesReachBatchCoreAndTypedArm()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray(); var expected = BuiltInFormulaReferences.EvenBetterSineValues(selected.Select(v => Candle(v)).ToArray(), 20, 5);
        var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersEvenBetterSineWaveIndicator(20, 5); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected); using var arm = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersEvenBetterSineWaveIndicator, new EhlersEvenBetterSineWaveIndicatorSpecOptions(20, 5), "Ebsi"), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
    }
    [Fact]
    public void InvalidFieldsLeaveHighPassLowPassAndSampleHistoryUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersEvenBetterSineWaveIndicatorState(); var control = new EhlersEvenBetterSineWaveIndicatorState(); var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
