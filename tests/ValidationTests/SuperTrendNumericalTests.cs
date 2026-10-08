using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class SuperTrendNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(SuperTrend)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStopSegments(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => new Dictionary<string, double[]> { { "Trend", BuiltInFormulaReferences.SuperTrendOutputs(bars, (IBuiltInIndicator)c.Factory()) } }, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod, double factor = 3)
    {
        var expected = BuiltInFormulaReferences.SuperTrendValues(bars, length, Kind(kind), factor); var batch = Data(bars).CalculateSuperTrend(kind, length, factor); Assert.Equal(expected.Values, batch.OutputValues["Trend"]); Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeSuperTrendFast(Data(bars), context, length, kind, factor); Assert.Equal(expected.Values, fast.ToArray()); using var state = new SuperTrendState(kind, length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Trend"]); } }
        }
        return expected.Values;
    }
    [Fact]
    public void WideRangesBothGapDirectionsAndRatchetsStayExact()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, 3 })
            Check(Enumerable.Range(0, 17).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, kind, factor);
        var gap = new[] { new Bar(DateTime.UnixEpoch, 15, 16, 14, 15, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 5, 8, 2, 5, 1) }; Assert.Equal(new[] { 13d, 17 }, Check(gap, 1, factor: 1));
        var reversed = gap.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume)).ToArray(); Assert.Equal(new[] { -17d, -17 }, Check(reversed, 1, factor: 1));
    }
    [Fact]
    public void ExtendedStopsCancelBeforePublicationAndZeroFactorsRemainFinite()
    {
        var prices = new[] { 0d, -double.MaxValue, double.MaxValue }; var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, p, 1)).ToArray(); Assert.Equal(new[] { -double.MaxValue, -double.MaxValue, 0 }, Check(bars, 1, factor: .5)); Assert.Equal(prices, Check(bars, 1, factor: 0)); Check(bars, 2, factor: double.MaxValue); Check(Bars(new[] { double.Epsilon, 2 * double.Epsilon, 0d, double.Epsilon }), 2, factor: double.MaxValue);
    }
    [Fact]
    public void HandStopsPreserveStrictCrossingsEqualityAndFirstDirection()
    {
        Assert.Equal(new[] { 80d, 80, 100, 50 }, Check(Bars(new[] { 80d, 100, 50, 120 }), 1, factor: 1));
        Assert.Equal(new[] { 13d, 16, 11 }, Check(new[] { 15d, 0, 5 }.Select((price, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + 1, price - 1, price, 1)).ToArray(), 1, factor: 1));
        var first = new[] { new Bar(DateTime.UnixEpoch, 15, 16, 14, 15, 1) }; Assert.Equal(new[] { 13d }, Check(first, 1, factor: 1)); Assert.Equal(Signal.StrongBuy, Data(first).CalculateSuperTrend(length: 1, atrMult: 1).SignalsList[0]); Assert.Equal(new[] { 13d }, Check(first, 1, factor: -1));
        Check(Bars(new[] { 0d, 2, 0, -2, 0, 2, 2, 0, -2, -2, 0 }), 1, factor: 1);
        var examples = new[] { (Prices: new[] { -4d, -2, -2, -4, -4 }, Stops: new[] { -6d, -5, -4, -4, -6 }), (Prices: new[] { -4d, 0, 0, 2, -4 }, Stops: new[] { -6d, -5, -2, -1, 3 }), (Prices: new[] { -4d, -4, -2, -2, -4 }, Stops: new[] { -6d, -6, -5, -4, -4 }), (Prices: new[] { -2d, 0, -4, -4, -2 }, Stops: new[] { -4d, -3, 1, -2, -2 }) };
        foreach (var example in examples) Assert.Equal(example.Stops, Check(example.Prices.Select((price, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + 1, price - 1, price, 1)).ToArray(), 1, factor: 1));
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepLazyAverageHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveRangesAndSignals()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.SuperTrendValues(effective, 3, 6, 3);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateSuperTrend(length: 3); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Values, data.CustomValuesList); using var context = new ComputeContext(); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var result = IndicatorCompute.ComputeSuperTrendFast(source, context, length: 3); Assert.Equal(expected.Values, result.ToArray());
    }
    [Fact]
    public void CustomAtrAndLegacyAveragesFeedBothStopCandidates()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 4, 8, 2, 4, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 5, 6, 3, 5, 1) };
        foreach (var fast in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 10d, 6, 3 }, values); return new[] { 1d, 2, 3 }; }); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeSuperTrendFast(Data(bars), context, length: 2, atrMult: 1); Assert.Equal(new[] { 4d, 4, 2 }, output.ToArray()); } else Assert.Equal(new[] { 4d, 4, 2 }, Data(bars).CalculateSuperTrend(length: 2, atrMult: 1).CustomValuesList); Assert.Equal(1, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var atr = Data(bars).CalculateAverageTrueRange(kind, 2).CustomValuesList; var lower = 0d; var upper = 0d; var up = true; var expected = new double[bars.Length];
        for (var i = 0; i < bars.Length; i++) { var price = bars[i].Close; var previous = i == 0 ? 0 : bars[i - 1].Close; var lo = price - 3 * atr[i]; var hi = price + 3 * atr[i]; var oldLo = i == 0 ? lo : lower; var oldHi = i == 0 ? hi : upper; lower = previous > oldLo ? Math.Max(lo, oldLo) : lo; upper = previous < oldHi ? Math.Min(hi, oldHi) : hi; up = up ? price >= oldLo : price > oldHi; expected[i] = up ? lower : upper; }
        var batch = Data(bars).CalculateSuperTrend(kind, 2); using var ctx = new ComputeContext(); using var result = IndicatorCompute.ComputeSuperTrendFast(Data(bars), ctx, 2, kind); for (var i = 0; i < bars.Length; i++) { Assert.Equal(expected[i], batch.CustomValuesList[i], 12); Assert.Equal(batch.CustomValuesList[i], result.Span[i]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAveragesStopsOrDirection()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new SuperTrendState(length: 2); using var control = new SuperTrendState(length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
    [Fact]
    public void NonfiniteFactorsRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateSuperTrend(atrMult: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new SuperTrendState(atrMult: factor)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeSuperTrendFast(data, context, atrMult: factor)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
