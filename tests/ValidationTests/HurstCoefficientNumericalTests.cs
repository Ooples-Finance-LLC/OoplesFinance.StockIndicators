using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HurstCoefficientNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select(p => new Bar(DateTime.UnixEpoch, p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersHurstCoefficient)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangesAndPolePolynomial(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.HurstCoefficientOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals, double[] Dimensions, double[] Hurst) Check(Bar[] bars, int length = 4, int smooth = 3)
    {
        var expected = BuiltInFormulaReferences.HurstCoefficientValues(bars, length, smooth);
        var batch = Data(bars).CalculateEhlersHurstCoefficient(length, smooth); Assert.Equal(expected.Outputs["Ehc"], batch.CustomValuesList); Assert.Equal(expected.Outputs["Ehc"], batch.OutputValues["Ehc"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersHurstCoefficientFast(Data(bars), context, length, smooth); Assert.Equal(expected.Outputs["Ehc"], fast.ToArray());
        using var state = new EhlersHurstCoefficientState(length, smooth); var window = new HurstCoefficientWindow(length, smooth);
        for (var pass = 0; pass < 2; pass++)
        {
            if (pass != 0) { state.Reset(); window.Reset(); }
            for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
            {
                var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(bars[i].Close, final); Assert.Equal(expected.Outputs["Ehc"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ehc"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Trade); Assert.Equal(expected.Dimensions[i], direct.Dimension); Assert.Equal(expected.Hurst[i], direct.Hurst);
            }
        }
        return expected;
    }
    [Fact]
    public void HandRangesRetainSubnormalAndOverflowingHalfSums()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var result = Check(Bars(new[] { 0d, scale, 0, scale, 0, scale }));
            Assert.Equal(new[] { 0d, 1, 1, 1.5, 1.75, 1.875 }, result.Dimensions); Assert.Equal(new[] { 2d, 1, 1, .5, .25, .125 }, result.Hurst);
        }
        Check(Bars(new[] { 0d, -1, 0, -1, 0, -1 }));
        // A step separates the older half from the current half; a period-two
        // alternating input cannot detect accidentally feeding current prices to both.
        var step = Check(Bars(new[] { 0d, 0, 1, 1 }));
        Assert.Equal(new[] { 0d, 0, .5, .5 }, step.Dimensions);
        Assert.Equal(new[] { 2d, 2, 1.5, 1.5 }, step.Hurst);
        // Before the older half fills, its missing sample remains zero.
        var positiveStep = Check(Bars(new[] { 1d, 1, 2, 2 }));
        Assert.Equal(new[] { 0d, 0, 1, 1 }, positiveStep.Dimensions);
        Assert.Equal(new[] { 2d, 2, 1, 1 }, positiveStep.Hurst);
    }
    [Fact]
    public void WideAlternatingAndNarrowPedestalPricesRecover()
    {
        foreach (var length in new[] { 3, 4, 7, 10 }) foreach (var smooth in new[] { 1, 3, 20, int.MaxValue })
            Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.Epsilon, -double.Epsilon, 1, 2, -1, 5, 2, 0, -2, 7, 3 }), length, smooth);
        Check(Bars(Enumerable.Range(0, 35).Select(i => i % 3 == 0 ? Math.BitIncrement(double.MaxValue / 2) : double.MaxValue / 2)), 9, 7);
        Check(Bars(Enumerable.Repeat(double.MaxValue, 40)), 8, 20);
        Check(Bars(new[] { 0d, 1, 0, 1 }.Concat(Enumerable.Repeat(1d, 12))), 4, 3);
        Check(Bars(Enumerable.Range(0, 31).Select(i => (i % 7 - 3) * Math.Pow(2, -540))), 5, 7);
    }
    [Fact]
    public void OneAndTwoPricePeriodsHaveZeroFractalRanges()
    {
        foreach (var length in new[] { 1, 2 })
        {
            var varying = Check(Bars(new[] { -double.MaxValue, double.MaxValue, -1, 3, 0, 7 }), length);
            var flat = Check(Bars(new double[6]), length); Assert.Equal(flat.Outputs["Ehc"], varying.Outputs["Ehc"]); Assert.All(varying.Dimensions, d => Assert.Equal(0, d)); Assert.All(varying.Hurst, h => Assert.Equal(2, h));
        }
    }
    [Fact]
    public void ExtremePeriodsUseLazyHistoryAndRetainSmallDcGain()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var smooth in new[] { int.MinValue, 0, 1, int.MaxValue })
        { Check(Array.Empty<Bar>(), length, smooth); Check(Bars(new[] { -double.MaxValue, double.MaxValue, 0, 3, -2 }), length, smooth); }
        var value = Check(Bars(new[] { 7d }), 1, int.MaxValue).Outputs["Ehc"][0]; var a = ReferenceFraction.FromDouble(Math.Exp(-Math.Sqrt(2) * Math.PI / int.MaxValue)); var expected = (new ReferenceFraction(1) - a) * (new ReferenceFraction(1) - a);
        Assert.True(value > 0); Assert.Equal(expected.ToDouble(), value);
    }
    [Fact]
    public void LogNormalizationHandlesBothExponentExtremes()
    {
        Assert.Equal(0, HurstCoefficientWindow.LogRatio(7, 7)); Assert.Equal(1, HurstCoefficientWindow.LogRatio(14, 7));
        Assert.Equal(2100, HurstCoefficientWindow.LogRatio(BigInteger.One << 2100, BigInteger.One)); Assert.Equal(-2100, HurstCoefficientWindow.LogRatio(BigInteger.One, BigInteger.One << 2100));
        Assert.Equal(1 + Math.Log(1.5) / Math.Log(2), HurstCoefficientWindow.LogRatio(3, 1)); Assert.Equal(-1 + Math.Log(1.5) / Math.Log(2), HurstCoefficientWindow.LogRatio(3, 4));
    }
    [Fact]
    public void SelectedInputsUseTheSameCausalRanges()
    {
        var bars = Bars(new[] { 2d, -1, 4, 3, -5, 7, 0, 1 }); var selected = new[] { 5d, 1, -3, 7, 0, -2, 4, 8 };
        var data = Data(bars); data.SetCustomValues(selected.ToList()); var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateEhlersHurstCoefficient(5, 3);
        var expected = BuiltInFormulaReferences.HurstCoefficientValues(Bars(selected), 5, 3).Outputs["Ehc"]; Assert.Equal(expected, batch.CustomValuesList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersHurstCoefficientFast(data, context, 5, 3); Assert.Equal(expected, fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
    }
    [Fact]
    public void NoMovingAverageOverrideIsConsumed()
    {
        var bars = Bars(new[] { 1d, 4, 0, 7, -3, 2 }); var expected = BuiltInFormulaReferences.HurstCoefficientValues(bars, 4, 3).Outputs["Ehc"]; var calls = 0;
        using var armed = ComponentAverage.Arm((values, _) => { calls++; return values; }); var batch = Data(bars).CalculateEhlersHurstCoefficient(4, 3);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEhlersHurstCoefficientFast(Data(bars), context, 4, 3); Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, fast.ToArray()); Assert.Equal(0, calls);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRangeOrRecursiveHistory()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersHurstCoefficientState(4, 3); using var control = new EhlersHurstCoefficientState(4, 3); foreach (var b in Bars(new[] { 1d, 4, 0 })) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var v = new[] { 2d, 4, 0, 2, 1 }; v[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, -2, 1 })) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Ehc"], actual.Outputs!["Ehc"]); }
        }
    }
}
