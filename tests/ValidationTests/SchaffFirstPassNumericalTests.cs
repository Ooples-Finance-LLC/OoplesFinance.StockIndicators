using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SchaffFirstPassNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("STC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(Stc) || c.IndicatorType == typeof(SchaffTrendCycle)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentMacdWindow(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.SchaffFirstPassOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int fastLength = 2, int slowLength = 5, int cycleLength = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.SchaffFirstPassValues(bars, fastLength, slowLength, cycleLength, Kind(kind)); var data = Data(bars).CalculateSchaffTrendCycle(kind, fastLength, slowLength, cycleLength);
        Assert.Equal(expected.Values, data.OutputValues["Stc"]); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); Assert.All(expected.Values, value => Assert.InRange(value, 0, 100));
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeSchaffTrendCycleFast(Data(bars), context, cycleLength: cycleLength, fastLength: fastLength, slowLength: slowLength, maType: kind); Assert.Equal(expected.Values, fast.ToArray());
        using var state = new SchaffTrendCycleState(kind, fastLength, slowLength, cycleLength);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Stc"]); } }
        }
        return expected.Values;
    }
    private static double[] External(double[] fast, double[] slow, int cycle)
    {
        var bars = Bars(Enumerable.Range(0, fast.Length).Select(i => (double)i)); var expected = BuiltInFormulaReferences.SchaffFirstPassValues(bars, 2, 5, cycle, 3, new[] { fast, slow });
        foreach (var fastRoute in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(calls == 0 ? 2 : 5, period); return calls++ == 0 ? fast : slow; }; using var armed = ComponentAverage.Arm(new[] { callback, callback }); using var context = new ComputeContext();
            if (fastRoute) { using var output = IndicatorCompute.ComputeSchaffTrendCycleFast(Data(bars), context, cycleLength: cycle, fastLength: 2, slowLength: 5); Assert.Equal(expected.Values, output.ToArray()); } else { var data = Data(bars).CalculateSchaffTrendCycle(fastLength: 2, slowLength: 5, cycleLength: cycle); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); } Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Substitutions);
        }
        return expected.Values;
    }
    [Fact]
    public void WideMeansAndMacdRangesPreserveBoundedNormalization()
    {
        foreach (var cycle in new[] { 1, 3, 7 }) foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Bars(new[] { 2d, 4, 6, 4, 2, 4, 6, -4, -6, -2, 3, 3, 5, -3, 0, 0 }.Select(p => p * scale)), 2, 5, cycle, kind);
    }
    [Fact]
    public void HandOscillatorUsesMacdWindowAndOneBarIsFlat()
    {
        Assert.Equal(new[] { 0d, 100, 0 }, Check(Bars(new[] { 1d, 3, 1 }), 1, 2, 2));
        Assert.All(Check(Bars(new[] { 1d, 3, 1, 7, -2, 9 }), 1, 2, 1), value => Assert.Equal(0, value));
        Assert.All(Check(Bars(new[] { 1d, 3, 1, 7, -2, 9 }), 3, 3, 4), value => Assert.Equal(0, value));
        Assert.Equal(new[] { 0d, 100, 0, 50 }, External(new[] { 1d, 1, -1, 0 }, new[] { 0d, -1, 1, 0 }, 3));
    }
    [Fact]
    public void SixtyFourEpsilonFlatnessBoundaryIsInclusive()
    {
        var boundary = Math.Pow(2, -45); Assert.Equal(new[] { 0d, 0, 0 }, External(new[] { 1d, 1, 1 }, new[] { -1d, -1 + boundary, -1 }, 3));
        Assert.Equal(new[] { 0d, 0, 100 }, External(new[] { 1d, 1, 1 }, new[] { -1d, -1 + 2 * boundary, -1 }, 3));
        Assert.Equal(new[] { 0d, 0, 0 }, External(new[] { -1d, -1 + boundary, -1 }, new[] { 1d, 1, 1 }, 3));
        Assert.Equal(new[] { 0d, 100, 0 }, External(new[] { -1d, -1 + 2 * boundary, -1 }, new[] { 1d, 1, 1 }, 3));
        Assert.All(Check(Bars(Enumerable.Range(0, 80).Select(i => 100 + .1 * i)), 4, 7, 10).Skip(17), value => Assert.Equal(0, value));
    }
    [Fact]
    public void ExtendedMeanDifferencesAndRangesNormalizeBeforeProjection()
    {
        var max = double.MaxValue; Assert.Equal(new[] { 0d, 100, 0, 50 }, External(new[] { max, max, -max, 0 }, new[] { max, -max, max, 0 }, 4));
        Check(Bars(new[] { -max, max, max, -max, 0d, max, 0 }), 1, 2, 3, MovingAvgType.SimpleMovingAverage);
        Assert.Equal(new[] { 0d, 0, 0, 100 }, External(new[] { max, 1d, 1, 1 }, new[] { max, 0d, 1, 0 }, 2));
        var prices = new[] { 2d, 4, 6, 2, -4, 1, 5, 0, 3, -1 }; var original = Check(Bars(prices)); foreach (var scale in new[] { Math.Pow(2, -600), Math.Pow(2, 600) }) Assert.Equal(original, Check(Bars(prices.Select(p => p * scale))));
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepEveryMeanAndDequeLazy()
    {
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds) foreach (var lengths in new[] { (period, 3, 2), (2, period, 3), (2, 3, period) })
        { Check(Array.Empty<Bar>(), lengths.Item1, lengths.Item2, lengths.Item3, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), lengths.Item1, lengths.Item2, lengths.Item3, kind); }
    }
    [Fact]
    public void CustomAndLegacyAveragesKeepFastSlowOrderAndPeriods()
    {
        var bars = Bars(new[] { 2d, 4, 6, 8 }); var selected = new[] { -2d, 0, 4, 3 }; var supplied = new[] { new[] { 1d, 1, -1, 0 }, new[] { 0d, -1, 1, 0 } }; var expected = BuiltInFormulaReferences.SchaffFirstPassValues(Bars(selected), 2, 5, 3, 3, supplied);
        foreach (var fastRoute in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(calls == 0 ? 2 : 5, period); Assert.Equal(selected, values); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback }); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (fastRoute) { using var output = IndicatorCompute.ComputeSchaffTrendCycleFast(data, context, cycleLength: 3, fastLength: 2, slowLength: 5); Assert.Equal(expected.Values, output.ToArray()); } else { data.CalculateSchaffTrendCycle(fastLength: 2, slowLength: 5, cycleLength: 3); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); } Assert.Equal(2, calls); Assert.Equal(2, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var price = bars.Select(b => b.Close).ToList(); var averages = new[] { CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, price).ToArray(), CalculationsHelper.GetMovingAverageList(Data(bars), kind, 5, price).ToArray() }; var reference = BuiltInFormulaReferences.SchaffFirstPassValues(bars, 2, 5, 3, 3, averages); var batch = Data(bars).CalculateSchaffTrendCycle(kind, 2, 5, 3); Assert.Equal(reference.Values, batch.CustomValuesList); Assert.Equal(reference.Signals, batch.SignalsList); using var ctx = new ComputeContext(); using var result = IndicatorCompute.ComputeSchaffTrendCycleFast(Data(bars), ctx, cycleLength: 3, fastLength: 2, slowLength: 5, maType: kind); Assert.Equal(reference.Values, result.ToArray());
    }
    [Fact]
    public void AliasFastRouteUsesCycleLengthAndSelectedInput()
    {
        var bars = Bars(Enumerable.Range(0, 80).Select(i => (double)(i % 11))); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 7 - 3d) * 3).ToArray();
        using var context = new ComputeContext(); foreach (var cycle in new[] { 1, 3, 10 }) { var data = Data(bars); data.SetCustomValues(selected.ToList()); var expected = BuiltInFormulaReferences.SchaffFirstPassValues(Bars(selected), 23, 50, cycle, 3).Values; using var alias = IndicatorCompute.ComputeSchaffTrendCycleFast(data, context, length: cycle); using var full = IndicatorCompute.ComputeSchaffTrendCycleFast(data, context, cycleLength: cycle); Assert.Equal(expected, alias.ToArray()); Assert.Equal(expected, full.ToArray()); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMeansExtremaOrSignals()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new SchaffTrendCycleState(fastLength: 2, slowLength: 3, cycleLength: 2); using var control = new SchaffTrendCycleState(fastLength: 2, slowLength: 3, cycleLength: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
}
