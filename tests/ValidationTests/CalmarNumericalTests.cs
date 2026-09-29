using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CalmarNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(CalmarRatio)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAnnualizationPolynomial(IndicatorValidationCase c, string route)
    {
        var options = (CalmarRatioSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions(); new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.CalmarValues(bars, options.Length).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length)
    {
        var expected = BuiltInFormulaReferences.CalmarValues(bars, length); var batch = Data(bars).CalculateCalmarRatio(length); Assert.Equal(new[] { "Cr" }, batch.OutputValues.Keys); Assert.Equal(expected.Outputs["Cr"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeCalmarRatioFast(Data(bars), context, length); Assert.Equal(expected.Outputs["Cr"], fast.ToArray()); using var state = new CalmarRatioState(length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 1d, 8, -2, 4, 3, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Cr"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Cr"]); } }
        }
        return expected.Outputs["Cr"];
    }
    [Fact]
    public void WideSignedAndSubnormalPricesPreserveReturnNormalization()
    {
        foreach (var length in new[] { 1, 2, 5, 30 }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
            Check(Bars(Enumerable.Range(0, 39).Select(i => (i % 7 - 3d) / 4 * scale)), length);
    }
    [Fact]
    public void ExtremeIntermediatePowersCancelAgainstDrawdownBeforePublication()
    {
        var prices = Enumerable.Repeat(double.Epsilon, 31).ToArray(); prices[0] = -double.Epsilon; prices[30] = -double.MaxValue / 4; var values = Check(Bars(prices), 30);
        Assert.True(values[30] > 0 && double.IsFinite(values[30])); Assert.True(values[30] < 1e-100);
        prices[0] = -double.MaxValue / 4; prices[30] = -double.Epsilon; Check(Bars(prices), 30);
    }
    [Fact]
    public void NearUnityReturnsRetainDirectionAndPowerOfTwoScaling()
    {
        foreach (var last in new[] { 1 + Math.Pow(2, -52), 1 - Math.Pow(2, -53) })
        {
            var prices = Enumerable.Repeat(2d, 31).ToArray(); prices[0] = prices[29] = 1; prices[30] = last; var expected = Check(Bars(prices), 30); Assert.Equal(Math.Sign(last - 1), Math.Sign(expected[30])); Assert.NotEqual(0d, expected[30]);
            foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) Assert.Equal(expected, Check(Bars(prices.Select(p => p * scale)), 30));
        }
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsKeepLazyHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), length); Assert.All(Check(Bars(new double[8]), length), v => Assert.Equal(0d, v)); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4, -1 }), length); }
    }
    [Fact]
    public void HandExamplesRetainAnnualizationAndZeroPolicies()
    {
        Assert.Equal(new[] { 0d, 0, 2 * (1d / 4096 - 1) }, Check(Bars(new[] { 2d, 1, 1 }), 2));
        Assert.Equal(new[] { 0d, 0, 0 }, Check(Bars(new[] { 0d, 1, 0 }), 2));
        Assert.Equal(new[] { 0d, 0, 2 * (531441d / 4096 - 1) }, Check(Bars(new[] { 2d, 1, 3 }), 2));
        Assert.Equal(new[] { 0d, 0, 0 }, Check(Bars(new[] { 0d, 1, 2 }), 2)); Assert.Equal(new[] { 0d, 0, 0 }, Check(Bars(new[] { 1d, 2, 3 }), 2)); Assert.Equal(new[] { 0d, 0, 0 }, Check(Bars(new[] { 1d, 2, -2 }), 2)); Assert.Equal(new[] { 0d, 0, -1 }, Check(Bars(new[] { 2d, 1, 0 }), 2));
    }
    [Fact]
    public void RollingPeaksAndDrawdownsExpireAtTheirOwnHorizons()
    {
        Assert.Equal(new[] { 0d, 2 * (Math.Pow(2, -24) - 1), 0 }, Check(Bars(new[] { 4d, 2, 3 }), 1));
        var tail = new[] { 1d, 3, 2, 5, 1, 4, 2, 6, 1, 3, 2 }; var values = Check(Bars(new[] { double.MaxValue / 4, double.MaxValue / 8 }.Concat(tail)), 3); Assert.Equal(Check(Bars(tail), 3).Skip(6), values.Skip(8));
    }
    [Fact]
    public void SelectedPricesReachAnnualReturnDrawdownAndSignals()
    {
        var belowThreshold = Check(Bars(new[] { 2d, 1, 2.1 }), 2); Assert.True(belowThreshold[2] > 0 && belowThreshold[2] < 2);
        var prices = Enumerable.Range(0, 39).Select(i => 2 + Math.Sin(i * .37)).ToArray(); var expected = BuiltInFormulaReferences.CalmarValues(Bars(prices), 5); var original = Bars(Enumerable.Repeat(100d, prices.Length)); var data = Data(original); data.SetCustomValues(prices.ToList()); data.CalculateCalmarRatio(5); Assert.Equal(expected.Outputs["Cr"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        var source = Data(original); source.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeCalmarRatioFast(source, context, 5); Assert.Equal(expected.Outputs["Cr"], result.ToArray());
    }
    [Fact]
    public void InvalidCandlesCannotAdvancePriceOrDrawdownWindows()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new CalmarRatioState(2); using var control = new CalmarRatioState(2); foreach (var bar in Bars(new[] { 1d, 3, 8, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
