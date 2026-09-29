using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class HalfTrendNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("HALF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(HalfTrend)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentConfirmedSegments(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.HalfTrendOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static double[] Check(Bar[] bars, int length = 2, int atrLength = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage)
    {
        var expected = BuiltInFormulaReferences.HalfTrendValues(bars, length, atrLength, Kind(kind)); var data = Data(bars).CalculateHalfTrend(kind, length, atrLength);
        Assert.Equal(expected.Values, data.OutputValues["Ht"]); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeHalfTrendFast(Data(bars), context, length, kind); Assert.Equal(expected.Values, fast.ToArray());
        using var state = new HalfTrendState(kind, length, atrLength);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Values[i], point.Value); Assert.Equal(expected.Values[i], point.Outputs!["Ht"]); } }
        }
        return expected.Values;
    }
    [Fact]
    public void WideMeansAndRetainedExtremesPreserveConfirmedReversals()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 31).Select(i => { var price = (i < 10 ? i * 1.5 - 6 : i < 20 ? 24 - i * 1.5 : i * 1.5 - 36) * scale; return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + .5 * scale, price - .5 * scale, price, 1); }).ToArray(), length, length + 1, kind);
    }
    [Fact]
    public void HandStopsPreserveInitialSupportAndConfirmedReversals()
    {
        var bars = new[] { 2d, 4, 6, 4, 2, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + .5, v - .5, v, 1)).ToArray(); Assert.Equal(new[] { 1.5, 3.5, 5.5, 5.5, 2.5, 2.5, 5.5 }, Check(bars, 1));
        Assert.Equal(new[] { 5d, 5, 5, 5 }, Check(Bars(new[] { 5d, 3, 1, -1 }), 1));
        Check(Bars(new[] { 0d, 0, 2, 2, 0, 0, -2, -2, 0, 0, 2, -2, 2 }), 1);
        Check(Bars(new[] { -2d, 0, 2, 0, -2, -2, 0, 2, 2, 0, -2 }), 2);
        foreach (var prices in new[] { new[] { 2d, 4, 6 }, new[] { 2d, 4, 2, 0 } }) Check(prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(), 1);
        // A high mean equal to retained support cannot confirm a falling reversal,
        // even when the close has crossed below the preceding low.
        var equalSupport = new[] { (High: 2d, Low: 0d, Close: 1d), (5d, 3d, 4d), (3d, 1d, 2d) }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Item3, v.Item1, v.Item2, v.Item3, 1)).ToArray();
        Assert.Equal(new[] { 0d, 3, 3 }, Check(equalSupport, 1, 1));
        Assert.All(Data(equalSupport).CalculateHalfTrend(length: 1, atrLength: 1).SignalsList, signal => Assert.Equal(Signal.None, signal));
    }
    [Fact]
    public void ExtendedAtrKeepsArrowEqualityAndDirectionExact()
    {
        var buyZero = Bars(new[] { 1d, 3, 1, 7 }); Assert.Equal(new[] { 1d, 3, 3, 3 }, Check(buyZero, 1, 1)); Assert.Equal(new[] { Signal.None, Signal.None, Signal.Sell, Signal.None }, Data(buyZero).CalculateHalfTrend(length: 1, atrLength: 1).SignalsList);
        var sellZero = Bars(new[] { -5d, -3, -9 }); Assert.Equal(new[] { -5d, -3, -3 }, Check(sellZero, 1, 1)); Assert.All(Data(sellZero).CalculateHalfTrend(length: 1, atrLength: 1).SignalsList, signal => Assert.Equal(Signal.None, signal));
        Check(Bars(new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, 0, double.MaxValue, -double.MaxValue }), 1, 1); Check(Bars(new[] { double.Epsilon, 2 * double.Epsilon, 0d, 3 * double.Epsilon, 0 }), 1, 1);
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepAveragesAndExtremaLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveHighLowRangesAndSignals()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.HalfTrendValues(effective, 3, 100, 1);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateHalfTrend(length: 3); Assert.Equal(expected.Values, data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); using var context = new ComputeContext(); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var fast = IndicatorCompute.ComputeHalfTrendFast(source, context, 3); Assert.Equal(expected.Values, fast.ToArray());
    }
    [Fact]
    public void CustomAveragesKeepAtrHighLowOrderAndPeriods()
    {
        var bars = new[] { 2d, 4, 0 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); var supplied = new[] { new[] { 1d, 2, 3 }, new[] { -10d, -10, -10 }, new[] { 10d, 10, 10 } }; var inputs = new[] { new[] { 2d, 3, 5 }, new[] { 3d, 5, 1 }, new[] { 1d, 3, -1 } };
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(calls == 0 ? 100 : 1, period); Assert.Equal(inputs[calls], values); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext();
            if (fast) { using var result = IndicatorCompute.ComputeHalfTrendFast(Data(bars), context, 1); Assert.Equal(new[] { 1d, 3, 3 }, result.ToArray()); } else { var data = Data(bars).CalculateHalfTrend(length: 1); Assert.Equal(new[] { 1d, 3, 3 }, data.CustomValuesList); Assert.Equal(new[] { Signal.None, Signal.None, Signal.Sell }, data.SignalsList); } Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void LegacyAverageComponentsRetainTheirPublishedStages()
    {
        var bars = new[] { 2d, 4, 6, 4, 2, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + .5, v - .5, v, 1)).ToArray(); var kind = MovingAvgType.DoubleExponentialMovingAverage;
        var external = new[] { Data(bars).CalculateAverageTrueRange(kind, 3).CustomValuesList.ToArray(), CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, bars.Select(b => b.High).ToList()).ToArray(), CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, bars.Select(b => b.Low).ToList()).ToArray() }; var expected = BuiltInFormulaReferences.HalfTrendValues(bars, 2, 3, 1, external); var batch = Data(bars).CalculateHalfTrend(kind, 2, 3); Assert.Equal(expected.Values, batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeHalfTrendFast(Data(bars), context, 2, kind); Assert.Equal(expected.Values, fast.ToArray());
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMeansExtremaOrDirection()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new HalfTrendState(length: 2); using var control = new HalfTrendState(length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) Assert.Equal(control.Update(Native(bar), true, false).Value, state.Update(Native(bar), true, false).Value);
        }
    }
}
