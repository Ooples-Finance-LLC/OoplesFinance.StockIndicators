using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class OneLcNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("OLC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(OneLCLeastSquaresMovingAverage) || c.IndicatorType == typeof(_1LCLeastSquaresMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCenteredCovariance(IndicatorValidationCase c, string route)
    {
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var settings = options is OneLCLeastSquaresMovingAverageSpecOptions named ? (named.Length, named.MaType) : options is _1LCLeastSquaresMovingAverageSpecOptions alias ? (alias.Length, alias.MaType) : throw new InvalidOperationException();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.OneLcValues(bars, settings.Item1, Kind(settings.Item2)).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static double[] Check(Bar[] bars, int length, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.OneLcValues(bars, length, Kind(kind)); var batch = Data(bars).Calculate1LCLeastSquaresMovingAverage(kind, length); Assert.Equal(new[] { "1lsma" }, batch.OutputValues.Keys); Assert.Equal(expected.Outputs["1lsma"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeOneLCLeastSquaresMovingAverageFast(Data(bars), context, length, kind); Assert.Equal(expected.Outputs["1lsma"], fast.ToArray());
        using var state = new _1LCLeastSquaresMovingAverageState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 1d, 8, -2, 4, 3, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -double.MaxValue })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["1lsma"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["1lsma"]); } }
        }
        return expected.Outputs["1lsma"];
    }
    [Fact]
    public void WideAndSubnormalCenteredPricesRetainCorrection()
    {
        foreach (var length in new[] { 1, 2, 5, 14 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue })
            Check(Bars(Enumerable.Range(0, 39).Select(i => (i % 7 - 3d) / 4 * scale)), length, kind);
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsUseLazyHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new double[7]), length, kind); Check(Bars(new[] { 1d, 3, 2, -4, 8, -2, 0 }), length, kind); }
    }
    [Fact]
    public void TwoPointHandExamplesKeepPopulationScalingAndStartup()
    {
        Assert.Equal(new[] { 0d, 3.7, 5.7, 7.7 }, Check(Bars(new[] { 1d, 3, 5, 7 }), 2));
        Assert.Equal(new[] { 0d, -3.7, -5.7, -7.7 }, Check(Bars(new[] { -1d, -3, -5, -7 }), 2));
        Assert.Equal(new[] { 1d, 3, -5, 7 }, Check(Bars(new[] { 1d, 3, -5, 7 }), 1));
    }
    [Fact]
    public void HistoricalPricesExpireBeforeLaterSmallWindows()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage })
        { var values = Check(Bars(new[] { double.MaxValue, -double.MaxValue, 0d, 0, 0, 1, 3, 5, 7, 9 }), 3, kind); Assert.Equal(Check(Bars(new[] { 1d, 3, 5, 7, 9 }), 3, kind).Skip(2), values.Skip(7)); }
    }
    [Fact]
    public void PowerOfTwoScalingPreservesRoundedTrajectory()
    {
        var source = Enumerable.Range(0, 43).Select(i => Math.Sin(i * .37) / 4).ToArray(); var expected = Check(Bars(source), 7);
        foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) Assert.Equal(expected.Select(v => v * scale), Check(Bars(source.Select(v => v * scale)), 7));
    }
    [Fact]
    public void SelectedPricesReachBothFastAliasesAndSignals()
    {
        var selected = Enumerable.Range(0, 37).Select(i => Math.Sin(i * .37)).ToArray(); var original = Bars(Enumerable.Repeat(100d, selected.Length)); var expected = BuiltInFormulaReferences.OneLcValues(Bars(selected), 5, 2); var data = Data(original); data.SetCustomValues(selected.ToList()); data.Calculate1LCLeastSquaresMovingAverage(MovingAvgType.WeightedMovingAverage, 5); Assert.Equal(expected.Outputs["1lsma"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        foreach (var alias in new[] { false, true }) { var source = Data(original); source.SetCustomValues(selected.ToList()); using var context = new ComputeContext(); using var result = alias ? IndicatorCompute.ComputeOneLCLeastSquaresFast(source, context, 5, MovingAvgType.WeightedMovingAverage) : IndicatorCompute.ComputeOneLCLeastSquaresMovingAverageFast(source, context, 5, MovingAvgType.WeightedMovingAverage); Assert.Equal(expected.Outputs["1lsma"], result.ToArray()); }
    }
    [Fact]
    public void CustomAverageIsConsumedOnceWithoutChangingCovariance()
    {
        var prices = Enumerable.Range(0, 31).Select(i => Math.Sin(i * .37)).ToArray(); var bars = Bars(prices); var mean = Enumerable.Range(0, prices.Length).Select(i => i % 2 == 0 ? 1000d : -1000d).ToArray(); var expected = BuiltInFormulaReferences.OneLcValues(bars, 5, externalAverage: mean);
        foreach (var route in new[] { "batch", "fast" })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(5, period); Assert.Equal(prices, values); return mean; });
            if (route == "batch") { var result = Data(bars).Calculate1LCLeastSquaresMovingAverage(length: 5); Assert.Equal(expected.Outputs["1lsma"], result.CustomValuesList); Assert.Equal(expected.Signals, result.SignalsList); }
            else { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeOneLCLeastSquaresMovingAverageFast(Data(bars), context, 5); Assert.Equal(expected.Outputs["1lsma"], result.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void OtherAveragesRetainConfiguredComponent()
    {
        var bars = Bars(Enumerable.Range(0, 45).Select(i => Math.Sin(i * .27))); var expected = BuiltInFormulaReferences.OneLcValues(bars, 5, 4).Outputs["1lsma"]; var batch = Data(bars).Calculate1LCLeastSquaresMovingAverage(MovingAvgType.DoubleExponentialMovingAverage, 5); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeOneLCLeastSquaresMovingAverageFast(Data(bars), context, 5, MovingAvgType.DoubleExponentialMovingAverage); using var state = new _1LCLeastSquaresMovingAverageState(MovingAvgType.DoubleExponentialMovingAverage, 5); var budget = new IndicatorErrorBudget(1e-12, 1e-12);
        for (var i = 0; i < bars.Length; i++) { Assert.True(budget.Accepts(expected[i], batch.CustomValuesList[i])); Assert.True(budget.Accepts(expected[i], fast.Span[i])); Assert.True(budget.Accepts(expected[i], state.Update(Native(bars[i]), true, false).Value)); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMomentsOrAverage()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new _1LCLeastSquaresMovingAverageState(length: 3); using var control = new _1LCLeastSquaresMovingAverageState(length: 3); foreach (var bar in Bars(new[] { 1d, 3, 8, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
