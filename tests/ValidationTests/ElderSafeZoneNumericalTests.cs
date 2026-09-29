using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ElderSafeZoneNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ElderSafeZoneStops)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStopSegments(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Eszs", BuiltInFormulaReferences.ElderSafeZoneOutputs(bars, (IBuiltInIndicator)c.Factory()) } }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int trendLength = 3, int noiseLength = 3, int stopLength = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, double factor = 2.5)
    {
        var expected = BuiltInFormulaReferences.ElderSafeZoneValues(bars, trendLength, noiseLength, stopLength, Kind(kind), factor); var batch = Data(bars).CalculateElderSafeZoneStops(kind, trendLength, noiseLength, stopLength, factor); Assert.Equal(expected.Values, batch.OutputValues["Eszs"]); Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeElderSafeZoneStopsFast(Data(bars), context, trendLength, noiseLength, stopLength, factor, kind); Assert.Equal(expected.Values, fast.ToArray()); using var state = new ElderSafeZoneStopsState(kind, trendLength, noiseLength, stopLength, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Eszs"]); } }
        }
        return expected.Values;
    }
    [Fact]
    public void WideEventMeansExtremaTrendAndSignalsStayExact()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, 2.5 })
            Check(Enumerable.Range(0, 19).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, length + 1, length, kind, factor);
    }
    [Fact]
    public void HandExamplesCountOnlyDirectionalEventsAndExpireStops()
    {
        var bars = new[] { 2d, 4, 1, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); Assert.Equal(new[] { 0d, 1, 0, -3 }, Check(bars, 1, 2, 1, factor: 1)); Assert.Equal(new[] { 0d, 1, 1, 1 }, Check(bars, 1, 2, 3, factor: 1));
        bars = new[] { -2d, -2, -4 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v + 1, v + 2, v, v + 1, 1)).ToArray(); Assert.Equal(new[] { -2d, -4, -4 }, Check(bars, 1, 3, 1, factor: 1)); Assert.Equal(new[] { 0d, -2, -2 }, Check(bars, 1, 3, 1, factor: 0));
    }
    [Fact]
    public void ExtendedDirectionalMovesAndZeroFactorsDoNotPoisonProjection()
    {
        var scale = Math.Pow(2, 1023); var bars = Bars(new[] { scale, -scale, scale, 0d, -scale, scale }); Check(bars, 2, 2, 1, factor: .5); Assert.Equal(new[] { 0d, scale, -scale, scale, 0, -scale }, Check(bars, 1, 1, 1, factor: 0));
        foreach (var factor in new[] { .5, double.MaxValue, -double.MaxValue }) Check(Bars(new[] { double.MaxValue, -double.MaxValue, 0d, double.MaxValue, 0, -double.MaxValue }), 3, 2, 2, factor: factor);
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepEveryHistoryLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, length, length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, length, length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveRangesAndBothSignalComparisons()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.ElderSafeZoneValues(effective, 3, 4, 2, 3, 2.5);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateElderSafeZoneStops(length1: 3, length2: 4, length3: 2); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Values, data.CustomValuesList); using var context = new ComputeContext(); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var result = IndicatorCompute.ComputeElderSafeZoneStopsFast(source, context, 3, 4, 2); Assert.Equal(expected.Values, result.ToArray());
    }
    [Fact]
    public void CustomTrendAndLegacyAveragesSelectTheCorrectNoiseSide()
    {
        var bars = new[] { 2d, 4, 1, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); var trend = new[] { 100d, -100, 100, -100 }; var target = new[] { 3d, 1, 7, -3 };
        foreach (var fast in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(2, period); Assert.Equal(bars.Select(b => b.Close), values); return trend; }); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeElderSafeZoneStopsFast(Data(bars), context, 2, 2, 1, 1); Assert.Equal(target, output.ToArray()); } else { var batch = Data(bars).CalculateElderSafeZoneStops(length1: 2, length2: 2, length3: 1, factor: 1); Assert.Equal(target, batch.CustomValuesList); Assert.Equal(BuiltInFormulaReferences.ElderSafeZoneValues(bars, 2, 2, 1, 3, 1, trend).Signals, batch.SignalsList); } Assert.Equal(1, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var legacy = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, bars.Select(b => b.Close).ToList()).ToArray(); var expected = BuiltInFormulaReferences.ElderSafeZoneValues(bars, 2, 2, 1, 3, 1, legacy); var data = Data(bars).CalculateElderSafeZoneStops(kind, 2, 2, 1, 1); using var ctx = new ComputeContext(); using var result = IndicatorCompute.ComputeElderSafeZoneStopsFast(Data(bars), ctx, 2, 2, 1, 1, kind); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Values, result.ToArray());
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMovesExtremaOrTrend()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new ElderSafeZoneStopsState(length1: 2, length2: 2, length3: 2); using var control = new ElderSafeZoneStopsState(length1: 2, length2: 2, length3: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
    [Fact]
    public void NonfiniteFactorsRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateElderSafeZoneStops(factor: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new ElderSafeZoneStopsState(factor: factor)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeElderSafeZoneStopsFast(data, context, factor: factor)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
