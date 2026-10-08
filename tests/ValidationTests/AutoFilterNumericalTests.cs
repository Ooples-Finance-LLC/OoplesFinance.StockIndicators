using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AutoFilterNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(AutoFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentCenteredRegression(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AutoFilterOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.AutoFilterValues(bars, length, Kind(kind)); var batch = Data(bars).CalculateAutoFilter(kind, length);
        Assert.Equal(expected.Outputs["Af"], batch.OutputValues["Af"]); Assert.Equal(expected.Outputs["Af"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeAutoFilterFast(Data(bars), context, length, kind); Assert.Equal(expected.Outputs["Af"], output.ToArray());
        using var state = new AutoFilterState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -4, 7, 1, -2, 8 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -91d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Af"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Af"]); } }
        }
        return expected.Outputs["Af"];
    }
    [Fact]
    public void WideDeviationCovarianceAndSelectedMeansMatchRationalStages()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 8 }) foreach (var kind in Kinds) foreach (var length in new[] { 1, 3, 7 })
            Check(Bars(Enumerable.Range(0, 24).Select(i => (i % 7 - 3 + (i % 3) * .125) * scale)), length, kind);
    }
    [Fact]
    public void HandRegressionAndFlatVariancePreserveWarmup()
    {
        var values = Check(Bars(new[] { 1d, 2, 4, 3.4 }), 3); Assert.Equal(new[] { 0d, 0, 4 }, values.Take(3)); Assert.Equal(3.6999999999999997, values[3]);
        Assert.Equal(new[] { 0d, 0, 5, 5 }, Check(Bars(Enumerable.Repeat(5d, 4)), 3));
        Assert.Equal(new[] { 10d / 3, 5, 5 }, Check(Bars(Enumerable.Repeat(5d, 3)), 2, MovingAvgType.WeightedMovingAverage));
        Assert.Equal(new[] { 2d, -3, 4 }, Check(Bars(new[] { 2d, -3, 4 }), 1));
    }
    [Fact]
    public void ExtremeMomentsExpireAndTinyOffsetsRemainObservable()
    {
        foreach (var kind in Kinds)
        {
            var values = Check(Bars(new[] { -double.MaxValue, double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 0 }), 3, kind);
            Assert.All(values, value => Assert.False(double.IsNaN(value))); Assert.True(double.IsFinite(values[^1]));
            Check(Bars(new[] { 1d, Math.BitIncrement(1), Math.BitIncrement(Math.BitIncrement(1)), 1, Math.BitDecrement(1), 1 }), 3, kind);
        }
    }
    [Fact]
    public void ExtremePeriodsKeepMomentsAndMeansLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds)
        { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, kind); }
    }
    [Fact]
    public void CustomMeansKeepPriceThenHeldStepOrder()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8, 10 }); var selected = new[] { 1d, 2, 4, 3.4, 3 }; var supplied = new[] { new[] { 1d, 2, 3, 4, 5 }, new[] { -1d, 0, 1, 2, 3 } };
        var expected = BuiltInFormulaReferences.AutoFilterValues(Bars(selected), 3, 1, supplied);
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(3, period); Assert.Equal(calls == 0 ? selected : expected.Steps, values); return supplied[calls++]; };
            using var armed = ComponentAverage.Arm(new[] { callback, callback }); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (fast) { using var output = IndicatorCompute.ComputeAutoFilterFast(data, context, 3); Assert.Equal(expected.Outputs["Af"], output.ToArray()); }
            else { data.CalculateAutoFilter(length: 3); Assert.Equal(expected.Outputs["Af"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
            Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Requests); Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void ExtendedCenteredOutputsRecoverWithoutPoisoningSignals()
    {
        var bars = Bars(new[] { 0d, 1, 2, 0, 0, 0 }); var supplied = new[] { new[] { 0d, 0, double.MaxValue, 0, 0, 0 }, new[] { 0d, 0, -double.MaxValue, 0, 0, 0 } }; var expected = BuiltInFormulaReferences.AutoFilterValues(bars, 2, 1, supplied);
        Assert.Equal(double.PositiveInfinity, expected.Outputs["Af"][2]); Assert.Equal(0, expected.Outputs["Af"][^1]); Assert.All(expected.Outputs["Af"], value => Assert.False(double.IsNaN(value)));
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (_, _) => supplied[calls++]; using var armed = ComponentAverage.Arm(new[] { callback, callback }); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeAutoFilterFast(Data(bars), context, 2); Assert.Equal(expected.Outputs["Af"], output.ToArray()); }
            else { var batch = Data(bars).CalculateAutoFilter(length: 2); Assert.Equal(expected.Outputs["Af"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList); } Assert.Equal(2, calls);
        }
    }
    [Fact]
    public void SelectedPricesAndLegacyMeansAgreeAcrossRoutes()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8, 10, 12 }); var selected = new[] { -2d, 0, 4, 3, -1, 8 }; var expected = BuiltInFormulaReferences.AutoFilterValues(Bars(selected), 3, 2); using var context = new ComputeContext();
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateAutoFilter(MovingAvgType.WeightedMovingAverage, 3); Assert.Equal(expected.Outputs["Af"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        var source = Data(bars); source.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeAutoFilterFast(source, context, 3, MovingAvgType.WeightedMovingAverage); Assert.Equal(expected.Outputs["Af"], output.ToArray());
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var legacy = Data(bars).CalculateAutoFilter(kind, 3); using var fast = IndicatorCompute.ComputeAutoFilterFast(Data(bars), context, 3, kind); Assert.Equal(legacy.CustomValuesList, fast.ToArray()); using var state = new AutoFilterState(kind, 3);
        for (var i = 0; i < bars.Length; i++) Assert.Equal(legacy.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMomentsMeansOrStep()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new AutoFilterState(length: 3); using var control = new AutoFilterState(length: 3);
            foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, true).Value, state.Update(Native(bar), true, true).Value);
        }
    }
}
