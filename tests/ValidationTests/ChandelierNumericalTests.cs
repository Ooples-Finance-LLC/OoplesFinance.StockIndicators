using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ChandelierNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ChandelierExit) || c.IndicatorType == typeof(ChandelierExitLong) || c.IndicatorType == typeof(ChandelierExitShort)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeProjection(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ChandelierOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static (double[] Long, double[] Short) Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod, double factor = 3)
    {
        var expected = BuiltInFormulaReferences.ChandelierValues(bars, length, Kind(kind), factor); var batch = Data(bars).CalculateChandelierExit(kind, length, factor); Assert.Equal(new[] { "ExitLong", "ExitShort" }, batch.OutputValues.Keys); Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var longs = IndicatorCompute.ComputeChandelierExitLongFast(Data(bars), context, length, kind, factor); using var shorts = IndicatorCompute.ComputeChandelierExitShortFast(Data(bars), context, length, kind, factor);
        Assert.Equal(expected.Outputs["ExitLong"], batch.OutputValues["ExitLong"]); Assert.Equal(expected.Outputs["ExitShort"], batch.OutputValues["ExitShort"]); Assert.Equal(expected.Outputs["ExitLong"], longs.ToArray()); Assert.Equal(expected.Outputs["ExitShort"], shorts.ToArray());
        using var state = new ChandelierExitState(kind, length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["ExitLong"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["ExitLong"]); Assert.Equal(expected.Outputs["ExitShort"][i], point.Outputs["ExitShort"]); } }
        }
        return (expected.Outputs["ExitLong"], expected.Outputs["ExitShort"]);
    }
    [Fact]
    public void WideRangesAndDistancesKeepBothProjectedStopsAndSignals()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, 3 })
            Check(Enumerable.Range(0, 17).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, kind, factor);
        var bars = new[] { new Bar(DateTime.UnixEpoch, 15, 16, 14, 15, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 5, 8, 2, 5, 1) }; var gap = Check(bars, 1, factor: 1); Assert.Equal(new[] { 14d, -5 }, gap.Long); Assert.Equal(new[] { 16d, 15 }, gap.Short);
    }
    [Fact]
    public void UnpublishedRangeAndProductOverflowCanCancelToFiniteStops()
    {
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, 0, 1)).ToArray();
        var result = Check(bars, 1, factor: .5); Assert.All(result.Long, v => Assert.Equal(0d, v)); Assert.All(result.Short, v => Assert.Equal(0d, v));
        result = Check(bars, 1, factor: 0); Assert.All(result.Long, v => Assert.Equal(double.MaxValue, v)); Assert.All(result.Short, v => Assert.Equal(-double.MaxValue, v));
        Check(Bars(new[] { double.Epsilon, 2 * double.Epsilon, 0d, double.Epsilon }), 2, factor: double.MaxValue);
        Check(bars, 2, factor: double.MaxValue);
    }
    [Fact]
    public void HandRangesUseCurrentFirstCloseAndExpireBothExtrema()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 4, 8, 2, 4, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 5, 6, 3, 5, 1) };
        var result = Check(bars, 2, MovingAvgType.SimpleMovingAverage); Assert.Equal(new[] { 10d, -14, -5.5 }, result.Long); Assert.Equal(new[] { 0d, 24, 15.5 }, result.Short);
        var first = Check(new[] { new Bar(DateTime.UnixEpoch, 15, 11, 9, 15, 1) }, 1, factor: 1); Assert.Equal(5, first.Long[0]); Assert.Equal(15, first.Short[0]);
        var tail = Bars(new[] { 1d, 3, 2, 5, 4, 2, 3, 1 }); var full = Check(Bars(new[] { 100d, -100 }).Concat(tail).ToArray(), 2, MovingAvgType.SimpleMovingAverage); var clean = Check(tail, 2, MovingAvgType.SimpleMovingAverage); Assert.Equal(clean.Long.Skip(4), full.Long.Skip(6)); Assert.Equal(clean.Short.Skip(4), full.Short.Skip(6));
    }
    [Fact]
    public void EmptyAndExtremePeriodsAllocateHistoryOnlyAsNeeded()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, kind); }
    }
    [Fact]
    public void SelectedPricesKeepOriginalRangesAndBothOutputSlots()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.ChandelierValues(effective, 3, 6, 3);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateChandelierExit(length: 3); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Outputs["ExitLong"], data.OutputValues["ExitLong"]); Assert.Equal(expected.Outputs["ExitShort"], data.OutputValues["ExitShort"]);
        var shortState = StatefulIndicatorFactory.Create(new IndicatorSpec(IndicatorName.ChandelierExit, new ChandelierExitShortSpecOptions(3))); Assert.NotNull(shortState); using var lifetime = shortState as IDisposable;
        foreach (var (bar, index) in bars.Select((bar, index) => (bar, index))) { var selected = effective[index]; var point = shortState.Update(Native(selected), true, false); Assert.Null(point.Outputs); Assert.Equal(expected.Outputs["ExitShort"][index], point.Value); }
        var source = Data(bars); source.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var longs = IndicatorCompute.ComputeChandelierExitLongFast(source, context, 3); using var shorts = IndicatorCompute.ComputeChandelierExitShortFast(source, context, 3); Assert.Equal(expected.Outputs["ExitLong"], longs.ToArray()); Assert.Equal(expected.Outputs["ExitShort"], shorts.ToArray());
    }
    [Fact]
    public void CustomAtrAndLegacyAverageRemainConsumedByEachRoute()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 4, 8, 2, 4, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 5, 6, 3, 5, 1) };
        foreach (var route in new[] { "batch", "long", "short" })
        {
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> component = (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 10d, 6, 3 }, values); return new[] { 1d, 2, 3 }; }; using var armed = ComponentAverage.Arm(new[] { component }); using var context = new ComputeContext();
            var expectedLong = new[] { 7d, 4, -1 }; var expectedShort = new[] { 3d, 6, 11 };
            if (route == "batch") { var data = Data(bars).CalculateChandelierExit(length: 2); Assert.Equal(expectedLong, data.OutputValues["ExitLong"]); Assert.Equal(expectedShort, data.OutputValues["ExitShort"]); }
            else { using var result = route == "long" ? IndicatorCompute.ComputeChandelierExitLongFast(Data(bars), context, 2) : IndicatorCompute.ComputeChandelierExitShortFast(Data(bars), context, 2); Assert.Equal(route == "long" ? expectedLong : expectedShort, result.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
        var legacy = MovingAvgType.DoubleExponentialMovingAverage; var atr = Data(bars).CalculateAverageTrueRange(legacy, 2).CustomValuesList; var batch = Data(bars).CalculateChandelierExit(legacy, 2); using var ctx = new ComputeContext(); using var fast = IndicatorCompute.ComputeChandelierExitLongFast(Data(bars), ctx, 2, legacy);
        for (var i = 0; i < bars.Length; i++) { Assert.Equal((i == 2 ? 8 : 10) - 3 * atr[i], batch.OutputValues["ExitLong"][i], 12); Assert.Equal(batch.OutputValues["ExitLong"][i], fast.Span[i]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceExtremaOrAverage()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new ChandelierExitState(length: 2); using var control = new ChandelierExitState(length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["ExitShort"], actual.Outputs!["ExitShort"]); }
        }
    }
    [Fact]
    public void NonfiniteMultipliersRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateChandelierExit(mult: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new ChandelierExitState(mult: factor)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeChandelierExitLongFast(data, context, mult: factor)); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeChandelierExitShortFast(data, context, mult: factor)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
