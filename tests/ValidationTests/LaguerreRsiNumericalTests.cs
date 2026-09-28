using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class LaguerreRsiNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersLaguerreRsi)).Select(c => new object[] { c });
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
    { var o = (EhlersLaguerreRsiSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return new() { { "Elrsi", BuiltInFormulaReferences.LaguerreRsiValues(bars, 1 - 2d / (Math.Max(1, o.Length) + 1d)) } }; }
    private static void Check(Bar[] bars, double gamma = .5)
    {
        var expected = BuiltInFormulaReferences.LaguerreRsiValues(bars, gamma); var batch = Data(bars).CalculateEhlersLaguerreRelativeStrengthIndex(gamma);
        Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues["Elrsi"]); Assert.All(expected, v => Assert.InRange(v, 0d, 1d));
        var core = new double[bars.Length]; MovingAverageCore.EhlersLaguerreRelativeStrengthIndex(bars.Select(b => b.Close).ToArray(), core, gamma); Assert.Equal(expected, core);
        var state = new EhlersLaguerreRelativeStrengthIndexState(gamma);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], actual.Value); Assert.Equal(expected[i], actual.Outputs!["Elrsi"]); }
            }
        }
    }
    [Fact]
    public void WideAndTinyValuesRetainFourStagesAndDirectionalSums()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var gamma in new[] { 0d, .2, .5, .95, 1d })
            Check(Enumerable.Range(0, 35).Select(i => Candle(i < 25 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), gamma);
    }
    [Fact]
    public void PowerOfTwoScalingPreservesTheEntireNormalizedTrajectory()
    {
        var bars = Enumerable.Range(0, 50).Select(i => Candle(i < 35 ? i % 5 - 2 : 0)).ToArray(); var expected = Data(bars).CalculateEhlersLaguerreRelativeStrengthIndex().CustomValuesList;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        { var scaled = bars.Select(b => Candle(b.Close * scale)).ToArray(); Assert.Equal(expected, Data(scaled).CalculateEhlersLaguerreRelativeStrengthIndex().CustomValuesList); Check(scaled); }
    }
    [Fact]
    public void FirstImpulseEndpointsAndConstantInputHaveKnownValues()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = new[] { Candle(0), Candle(scale) }; Assert.Equal(5d / 7, Data(bars).CalculateEhlersLaguerreRelativeStrengthIndex().CustomValuesList[1]); Check(bars);
            Assert.Equal(1d, Data(bars).CalculateEhlersLaguerreRelativeStrengthIndex(0).CustomValuesList[1]);
            Assert.Equal(0d, Data(bars).CalculateEhlersLaguerreRelativeStrengthIndex(1).CustomValuesList[1]);
            var constant = Enumerable.Repeat(Candle(scale), 12).ToArray(); Assert.All(Data(constant).CalculateEhlersLaguerreRelativeStrengthIndex().CustomValuesList, v => Assert.Equal(0d, v)); Check(constant);
        }
        var prices = Enumerable.Range(0, 10).Select(i => Candle(i % 3)).ToArray();
        foreach (var gamma in new[] { -double.MaxValue, -1d, 2d, double.MaxValue }) Check(prices, gamma);
    }
    [Fact]
    public void PeriodConversionNormalizesAndDoesNotOverflow()
    {
        var bars = Enumerable.Range(0, 20).Select(i => Candle(i % 3)).ToArray();
        foreach (var length in new[] { int.MinValue, 0, 1, 3, 14, int.MaxValue })
        {
            var gamma = 1 - 2d / (Math.Max(1, length) + 1d); var expected = BuiltInFormulaReferences.LaguerreRsiValues(bars, gamma);
            var spec = new IndicatorSpec(IndicatorName.EhlersLaguerreRelativeStrengthIndex, new EhlersLaguerreRsiSpecOptions(length), "Elrsi");
            using var context = new ComputeContext(); using var arm = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
            var state = StatefulIndicatorFactory.Create(spec); Assert.IsType<EhlersLaguerreRelativeStrengthIndexState>(state);
            Assert.Equal(expected, bars.Select(b => state.Update(Native(b), true, false).Value).ToArray());
            Check(bars, gamma); Check(Array.Empty<Bar>(), gamma);
        }
    }
    [Fact]
    public async Task MaximumPublicLengthPreservesBoundBatchAndNativeMapping()
    {
        var c = new IndicatorValidationCase(typeof(EhlersLaguerreRsi), "maximum-length", () => new EhlersLaguerreRsi(int.MaxValue));
        foreach (var route in new[] { "batch", "fast", "arm", "native", "streaming" }) new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    }
    [Fact]
    public void SelectedPricesReachBatchAndTypedArm()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.LaguerreRsiValues(selected.Select(v => Candle(v)).ToArray(), .5); var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersLaguerreRelativeStrengthIndex(); Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected); using var arm = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersLaguerreRelativeStrengthIndex, new EhlersLaguerreRsiSpecOptions(3), "Elrsi"), context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
    }
    [Fact]
    public void NonfiniteGammaIsRejectedEvenForEmptyInputs()
    {
        foreach (var gamma in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersLaguerreRelativeStrengthIndexState(gamma));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersLaguerreRelativeStrengthIndex(gamma));
            Assert.Throws<ArgumentOutOfRangeException>(() => MovingAverageCore.EhlersLaguerreRelativeStrengthIndex(Array.Empty<double>(), Array.Empty<double>(), gamma));
        }
    }
    [Fact]
    public void InvalidFieldsLeaveBaselineAndStageHistoryUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new EhlersLaguerreRelativeStrengthIndexState(); var control = new EhlersLaguerreRelativeStrengthIndexState(); var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
