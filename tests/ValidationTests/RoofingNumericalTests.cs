using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RoofingNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersRoofingFilterV2) || c.IndicatorType == typeof(EhlersRoofingFilterIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWindow(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (int Upper, int Lower, bool Original) Options(IIndicator indicator)
    { var o = ((IBuiltInIndicator)indicator).CreateOptions(); return o is EhlersRoofingFilterV2SpecOptions v ? (v.UpperLength, v.LowerLength, false) : (((EhlersRoofingFilterIndicatorSpecOptions)o).Length1, ((EhlersRoofingFilterIndicatorSpecOptions)o).Length2, true); }
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = Options(indicator); return new() { { o.Original ? "Erfi" : "Erf", BuiltInFormulaReferences.RoofingValues(bars, o.Upper, o.Lower, o.Original) } }; }
    private static void Check(Bar[] bars, int upper, int lower, bool original)
    {
        var expected = BuiltInFormulaReferences.RoofingValues(bars, upper, lower, original); var key = original ? "Erfi" : "Erf";
        var batch = original ? Data(bars).CalculateEhlersRoofingFilterIndicator(upper, lower) : Data(bars).CalculateEhlersRoofingFilterV2(upper, lower);
        Assert.Equal(expected, batch.CustomValuesList); Assert.Equal(expected, batch.OutputValues[key]);
        IIndicatorSpecOptions options = original ? new EhlersRoofingFilterIndicatorSpecOptions(upper, lower) : new EhlersRoofingFilterV2SpecOptions(upper, lower);
        var spec = new IndicatorSpec(original ? IndicatorName.EhlersRoofingFilterIndicator : IndicatorName.EhlersRoofingFilterV2, options, key);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.TryComputeFast(Data(bars), spec, context); Assert.NotNull(fast); Assert.Equal(expected, fast.Value.ToArray());
        if (!original) { var output = new double[bars.Length]; OscillatorCore.EhlersRoofingFilterV2(bars.Select(b => b.Close).ToArray(), output, upper, lower); Assert.Equal(expected, output); }
        var state = StatefulIndicatorFactory.Create(spec); using var lifetime = state as IDisposable;
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var p = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], p.Value); Assert.Equal(expected[i], p.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void WideAndSubnormalPricesPreserveBothGainVariants()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var periods in new[] { (1, 1), (1, 40), (80, 1), (80, 40) }) foreach (var original in new[] { false, true })
            Check(Enumerable.Range(0, 35).Select(i => Candle(i < 20 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), periods.Item1, periods.Item2, original);
    }
    [Fact]
    public void DcAndNyquistNumeratorsCancelWithoutIntermediateOverflow()
    {
        foreach (var original in new[] { false, true }) foreach (var alternating in new[] { false, true })
            Check(Enumerable.Range(0, 40).Select(i => Candle(alternating && i % 2 == 1 ? -double.MaxValue : double.MaxValue)).ToArray(), 20, 10, original);
    }
    [Fact]
    public void FirstImpulseRetainsDistinctGainsAndMaximumPeriodResponse()
    {
        foreach (var upper in new[] { 1, 80, int.MaxValue }) foreach (var lower in new[] { 1, 40, int.MaxValue })
        {
            var bars = new[] { Candle(1) }; var a = Data(bars).CalculateEhlersRoofingFilterIndicator(upper, lower).CustomValuesList[0];
            var b = Data(bars).CalculateEhlersRoofingFilterV2(upper, lower).CustomValuesList[0]; Assert.True(a > 0 && b > 0);
            var angle = Math.Min(Math.Sqrt(2) * Math.PI / upper, .99); var pole = Math.Cos(angle) / (1 + Math.Sin(angle));
            var ratio = Math.Pow((1 + pole) / pole, 2); Assert.InRange(Math.Abs(a / b - ratio), 0, 1e-12);
            Check(bars, upper, lower, false); Check(bars, upper, lower, true);
        }
    }
    [Fact]
    public void PeriodsNormalizeIndependentlyWithoutHistoryAllocation()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 3)).ToArray();
        foreach (var periods in new[] { (int.MinValue, 0), (0, int.MaxValue), (int.MaxValue, -1), (int.MaxValue, int.MaxValue) }) foreach (var original in new[] { false, true })
        { Check(bars, periods.Item1, periods.Item2, original); Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, original); }
    }
    [Fact]
    public void SelectedPricesAndIndependentPeriodsReachBothTypedRoutes()
    {
        var selected = Enumerable.Range(0, 30).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var original in new[] { false, true }) foreach (var periods in new[] { (1, 20), (20, 1), (20, 40), (int.MaxValue, int.MaxValue) })
        {
            var expected = BuiltInFormulaReferences.RoofingValues(selected.Select(v => Candle(v)).ToArray(), periods.Item1, periods.Item2, original);
            var batch = Data(bars); batch.SetCustomValues(selected);
            if (original) batch.CalculateEhlersRoofingFilterIndicator(periods.Item1, periods.Item2); else batch.CalculateEhlersRoofingFilterV2(periods.Item1, periods.Item2);
            Assert.Equal(expected, batch.CustomValuesList);
            IIndicatorSpecOptions options = original ? new EhlersRoofingFilterIndicatorSpecOptions(periods.Item1, periods.Item2) : new EhlersRoofingFilterV2SpecOptions(periods.Item1, periods.Item2);
            var spec = new IndicatorSpec(original ? IndicatorName.EhlersRoofingFilterIndicator : IndicatorName.EhlersRoofingFilterV2, options, original ? "Erfi" : "Erf");
            using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected); using var output = IndicatorCompute.TryComputeFast(data, spec, context); Assert.NotNull(output); Assert.Equal(expected, output.Value.ToArray());
            using var arm = IndicatorCompute.ComputeArm(data, spec, context); Assert.NotNull(arm); Assert.Equal(expected, arm.Value.ToArray());
        }
    }
    [Fact]
    public void InvalidFieldsLeaveAllRecursiveStagesUnchanged()
    {
        foreach (var original in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState Make() => original ? new EhlersRoofingFilterIndicatorState(20, 10) : new EhlersRoofingFilterV2State(20, 10);
            var state = Make(); var control = Make(); using var a = state as IDisposable; using var b = control as IDisposable;
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
