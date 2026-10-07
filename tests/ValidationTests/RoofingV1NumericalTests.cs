using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RoofingV1NumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersRoofingFilterV1) || c.IndicatorType == typeof(EhlersRoofingFilter)).Select(c => new object[] { c });
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
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = BuiltInFormulaReferences.RoofingV1Options(((IBuiltInIndicator)indicator).CreateOptions()); return new() { { "Erf", BuiltInFormulaReferences.RoofingV1Values(bars, o.High, o.Low, o.Kind) } }; }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.ExponentialMovingAverage => 3, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.WildersSmoothingMethod => 6, _ => 7 };
    private static readonly MovingAvgType[] Averages = { MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, MovingAvgType.SimpleMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static void Check(Bar[] bars, int high, int low, MovingAvgType kind)
    {
        var expected = BuiltInFormulaReferences.RoofingV1Values(bars, high, low, Kind(kind));
        Assert.Equal(expected, Data(bars).CalculateEhlersRoofingFilterV1(kind, high, low).CustomValuesList);
        var options = new EhlersRoofingFilterV1SpecOptions(high, low, kind); var spec = new IndicatorSpec(IndicatorName.EhlersRoofingFilterV1, options, "Erf");
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeArm(Data(bars), spec, context); Assert.NotNull(fast); Assert.Equal(expected, fast.Value.ToArray());
        if (kind == MovingAvgType.Ehlers2PoleSuperSmootherFilterV1)
        { var alias = new IndicatorSpec(IndicatorName.EhlersRoofingFilterV1, new EhlersRoofingFilterSpecOptions(high, low), "Erf"); using var output = IndicatorCompute.TryComputeFast(Data(bars), alias, context); Assert.NotNull(output); Assert.Equal(expected, output.Value.ToArray()); }
        var state = StatefulIndicatorFactory.Create(spec); using var lifetime = state as IDisposable;
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                { var p = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[i], p.Value); Assert.Equal(expected[i], p.Outputs!["Erf"]); }
            }
        }
    }
    [Fact]
    public void WideValuesStayExtendedThroughEverySupportedAverage()
    {
        foreach (var kind in Averages) foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
            Check(Enumerable.Range(0, 35).Select(i => Candle(i < 20 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), 48, 10, kind);
    }
    [Fact]
    public void LevelReversalRetainsUnpublishableHighPassThroughSmoothing()
    {
        var bars = Enumerable.Range(0, 120).Select(i => Candle(i < 90 ? double.MaxValue : -double.MaxValue)).ToArray();
        var raw = BuiltInFormulaReferences.RoofingV1Values(bars, 48, 1, 3);
        Assert.Contains(raw, double.IsInfinity);
        foreach (var kind in Averages) Check(bars, 48, 20, kind);
    }
    [Fact]
    public void DcAndNyquistNumeratorsCancelBeforeRecursivePoles()
    {
        foreach (var alternating in new[] { false, true }) foreach (var kind in Averages)
            Check(Enumerable.Range(0, 35).Select(i => Candle(alternating && i % 2 == 1 ? -double.MaxValue : double.MaxValue)).ToArray(), 20, 10, kind);
    }
    [Fact]
    public void PeriodsNormalizeIndependentlyAndHistoryGrowsAsNeeded()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 3)).ToArray();
        foreach (var kind in Averages) foreach (var periods in new[] { (int.MinValue, 0), (0, int.MaxValue), (int.MaxValue, -1), (int.MaxValue, int.MaxValue), (48, 1) })
        { Check(bars, periods.Item1, periods.Item2, kind); Check(Array.Empty<Bar>(), periods.Item1, periods.Item2, kind); }
    }
    [Fact]
    public void StartupCopiesThreeHighPassValuesAndHighPeriodOneIsZero()
    {
        var bars = new[] { Candle(1), Candle(0), Candle(0) };
        var reference = Data(bars).CalculateEhlersRoofingFilterV1(length1: 48, length2: 10).CustomValuesList;
        Assert.Equal(reference, Data(bars).CalculateEhlersRoofingFilterV1(length1: 48, length2: int.MaxValue).CustomValuesList);
        var tangent = Math.Tan(Math.PI / (48 * Math.Sqrt(2))); var alpha = 2 * tangent / (1 + tangent);
        Assert.Equal(Math.Pow(1 - alpha / 2, 2) / 2, reference[0]); Assert.True(reference[0] > 0);
        Assert.All(Data(bars).CalculateEhlersRoofingFilterV1(length1: 1).CustomValuesList, v => Assert.Equal(0d, v));
    }
    [Fact]
    public void SelectedPricesReachBatchAndFastRoutes()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var kind in Averages)
        {
            var expected = BuiltInFormulaReferences.RoofingV1Values(selected.Select(v => Candle(v)).ToArray(), 20, 5, Kind(kind));
            var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersRoofingFilterV1(kind, 20, 5); Assert.Equal(expected, batch.CustomValuesList);
            using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected);
            using var output = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersRoofingFilterV1, new EhlersRoofingFilterV1SpecOptions(20, 5, kind), "Erf"), context); Assert.NotNull(output); Assert.Equal(expected, output.Value.ToArray());
        }
    }
    [Fact]
    public void CallbackReceivesHighPassValuesOnceWithNormalizedPeriod()
    {
        var bars = new[] { Candle(1), Candle(3), Candle(2) }; var raw = BuiltInFormulaReferences.RoofingV1Values(bars, 48, 10, 7);
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(1, period); Assert.Equal(raw, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            var expected = Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Assert.Equal(expected, Data(bars).CalculateEhlersRoofingFilterV1(length2: 0).CustomValuesList);
            else { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeEhlersRoofingFilterV1Fast(Data(bars), context, length2: 0); Assert.Equal(expected, output.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsLeaveHighPassAndAveragesUnchanged()
    {
        foreach (var kind in Averages) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersRoofingFilterV1State(kind, 20, 10); using var control = new EhlersRoofingFilterV1State(kind, 20, 10);
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3)))) Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
