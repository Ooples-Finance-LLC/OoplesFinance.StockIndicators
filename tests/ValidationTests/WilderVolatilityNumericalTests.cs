using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class WilderVolatilityNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(WellesWilderVolatilitySystem)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeAndTrendProjection(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.WilderVolatilityOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int trendLength = 5, int rangeLength = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, double factor = 3)
    {
        var expected = BuiltInFormulaReferences.WilderVolatilityValues(bars, trendLength, rangeLength, Kind(kind), factor); var batch = Data(bars).CalculateWellesWilderVolatilitySystem(kind, trendLength, rangeLength, factor); Assert.Equal(new[] { "Wwvs" }, batch.OutputValues.Keys); Assert.Equal(expected.Outputs["Wwvs"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeWellesWilderVolatilitySystemFast(Data(bars), context, kind, trendLength, rangeLength, factor); Assert.Equal(expected.Outputs["Wwvs"], fast.ToArray()); using var state = new WellesWilderVolatilitySystemState(kind, trendLength, rangeLength, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Wwvs"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Wwvs"]); } }
        }
        return expected.Outputs["Wwvs"];
    }
    [Fact]
    public void WideRangesPreserveTrendDirectionExtremaAndStopProjection()
    {
        foreach (var periods in new[] { (1, 1), (3, 2), (5, 7) }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, 3 })
            Check(Enumerable.Range(0, 17).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), periods.Item1, periods.Item2, kind, factor);
        var bars = new[] { new Bar(DateTime.UnixEpoch, 15, 16, 14, 15, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 5, 8, 2, 5, 1) }; Assert.Equal(new[] { 17d, 18 }, Check(bars, 1, 1, factor: 1));
    }
    [Fact]
    public void UnpublishedRangesCancelAgainstBothExtremaBeforePublication()
    {
        var bars = new[] { -double.MaxValue, double.MaxValue }.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, p, 1)).ToArray();
        Assert.Equal(new[] { 0d, 0 }, Check(bars, 2, 1, factor: .5)); Assert.Equal(new[] { -double.MaxValue, double.MaxValue }, Check(bars, 2, 1, factor: 0)); Check(bars, 2, 1, factor: double.MaxValue);
        Check(Bars(new[] { double.Epsilon, 2 * double.Epsilon, 0d, double.Epsilon }), 3, 2, factor: double.MaxValue);
    }
    [Fact]
    public void EqualityUsesLowerSideAndExtremaKeepTwoBarMinimum()
    {
        Assert.Equal(new[] { 80d, 100, 100, 120 }, Check(Bars(new[] { 80d, 100, 50, 120 }), 1, 1, factor: 1));
        Assert.Equal(new[] { 1d, 1, 2 }, Check(Bars(new[] { 1d, 2, 3 }), 1, 1, factor: 0));
        var first = new[] { new Bar(DateTime.UnixEpoch, 15, 11, 9, 15, 1) }; Assert.Equal(21d, Check(first, 1, 1, factor: 1)[0]);
        var tail = Bars(new[] { 1d, 3, 2, 5, 4, 2, 3, 1 }); var full = Check(Bars(new[] { 100d, -100 }).Concat(tail).ToArray(), 3, 2, MovingAvgType.SimpleMovingAverage); Assert.Equal(Check(tail, 3, 2, MovingAvgType.SimpleMovingAverage).Skip(4), full.Skip(6));
    }
    [Fact]
    public void EmptyAndExtremePeriodsAllocateHistoryOnlyAsNeeded()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveRangesTrendAndSignals()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.WilderVolatilityValues(effective, 5, 3, 3, 3);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateWellesWilderVolatilitySystem(length1: 5, length2: 3); Assert.Equal(expected.Signals, data.SignalsList); Assert.Equal(expected.Outputs["Wwvs"], data.CustomValuesList);
        var source = Data(bars); source.SetCustomValues(prices.ToList()); using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeWellesWilderVolatilitySystemFast(source, context, trendLength: 5, rangeLength: 3); Assert.Equal(expected.Outputs["Wwvs"], result.ToArray());
    }
    [Fact]
    public void CustomRangeThenTrendAndLegacyAveragesKeepTheirOrder()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 4, 8, 2, 4, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 5, 6, 3, 5, 1) };
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 10d, 6, 3 }, values); return new[] { 1d, 2, 3 }; }, (values, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 5d, 4, 5 }, values); return new[] { 0d, 10, 0 }; } });
            var target = new[] { 2d, 10, -4 }; if (batch) Assert.Equal(target, Data(bars).CalculateWellesWilderVolatilitySystem(length1: 3, length2: 2).CustomValuesList); else { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeWellesWilderVolatilitySystemFast(Data(bars), context, trendLength: 3, rangeLength: 2); Assert.Equal(target, result.ToArray()); } Assert.Equal(2, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var atr = Data(bars).CalculateAverageTrueRange(kind, 2).CustomValuesList; var trend = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 3, bars.Select(b => b.Close).ToList()); var expected = bars.Select((b, i) => b.Close > trend[i] ? bars.Skip(Math.Max(0, i - 1)).Take(Math.Min(2, i + 1)).Max(v => v.Close) - 3 * atr[i] : bars.Skip(Math.Max(0, i - 1)).Take(Math.Min(2, i + 1)).Min(v => v.Close) + 3 * atr[i]).ToArray(); var data = Data(bars).CalculateWellesWilderVolatilitySystem(kind, 3, 2); using var ctx = new ComputeContext(); using var fast = IndicatorCompute.ComputeWellesWilderVolatilitySystemFast(Data(bars), ctx, kind, 3, 2); for (var i = 0; i < bars.Length; i++) { Assert.Equal(expected[i], data.CustomValuesList[i], 12); Assert.Equal(data.CustomValuesList[i], fast.Span[i]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceEitherAverageOrPriceExtrema()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new WellesWilderVolatilitySystemState(length1: 3, length2: 2); using var control = new WellesWilderVolatilitySystemState(length1: 3, length2: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
    [Fact]
    public void NonfiniteFactorsRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateWellesWilderVolatilitySystem(factor: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new WellesWilderVolatilitySystemState(factor: factor)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeWellesWilderVolatilitySystemFast(data, context, factor: factor)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
