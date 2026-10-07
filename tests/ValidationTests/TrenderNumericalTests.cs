using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TrenderNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("TRDR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(Trender)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDirectionalEvents(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TrenderOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly string[] Keys = { "TrendUp", "TrendDn", "Trender" };
    private static readonly IndicatorCompute.TrenderSeries[] Series = { IndicatorCompute.TrenderSeries.TrendUp, IndicatorCompute.TrenderSeries.TrendDown, IndicatorCompute.TrenderSeries.Trender };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, double factor = 2)
    {
        var expected = BuiltInFormulaReferences.TrenderValues(bars, length, Kind(kind), factor); var batch = Data(bars).CalculateTrender(kind, length, factor); Assert.Equal(Keys, batch.OutputValues.Keys); Assert.Equal(expected.Outputs["Trender"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); for (var slot = 0; slot < Keys.Length; slot++) { using var fast = IndicatorCompute.ComputeTrenderFast(Data(bars), context, length, factor, kind, Series[slot]); Assert.Equal(expected.Outputs[Keys[slot]], batch.OutputValues[Keys[slot]]); Assert.Equal(expected.Outputs[Keys[slot]], fast.ToArray()); }
        using var state = new TrenderState(kind, length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Trender"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideStagesPreserveEveryStopCrossingAndMoment()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, 2 })
            Check(Enumerable.Range(0, 19).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, kind, factor);
        // The downward gap makes low-to-previous-close (6) dominate range (2)
        // and high-to-previous-close (4). ATRs [0,4] have deviation2 and stop9.
        var gap = new[] { (High: 11d, Low: 9d, Close: 10d), (6d, 4d, 5d) }
            .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Item3, v.Item1, v.Item2, v.Item3, 1)).ToArray();
        var gapOutput = Check(gap, 2, MovingAvgType.SimpleMovingAverage, 2);
        Assert.Equal(new[] { 0d, 9 }, gapOutput["TrendDn"]); Assert.Equal(new[] { 0d, 9 }, gapOutput["Trender"]);
    }
    [Fact]
    public void HandStopsKeepTwoBarResetsAndEqualityCarry()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); var output = Check(bars, 1); Assert.Equal(new[] { 2d, 4, 4, 3 }, output["TrendUp"]); Assert.Equal(new[] { 0d, 0, 3, 3 }, output["TrendDn"]); Assert.Equal(new[] { 2d, 4, 3, 3 }, output["Trender"]);
        var equality = Check(Bars(new[] { 2d, 2, 2, 0, 0, 2, 2 }), 1); Assert.Equal(new[] { 0d, 0, 0, 0, 0, 2, 2 }, equality["Trender"]);
        Check(Bars(new[] { -2d, 0, 2, 0, -2, -2, 0, 2, 2, 0, -2 }), 2);
    }
    [Fact]
    public void ExtendedStagesCancelAndStopsRecoverAfterOverflow()
    {
        var max = double.MaxValue; Check(Bars(new[] { -max, max, 0d, max, -max, 0, 0, 0 }), 2, factor: 0); Check(Bars(new[] { -max, max, 0d, max, -max, 0, 0, 0 }), 2, factor: max);
        Check(Bars(new[] { double.Epsilon, 0d, 2 * double.Epsilon, 0 }), 2, factor: max);
        var prices = new[] { max / 8, max / 4, 0, max / 8, -max / 4, 0, 0, 0, 0 }; var output = Check(Bars(prices), 2, MovingAvgType.SimpleMovingAverage, max); Assert.All(output.Values, values => Assert.All(values.Skip(6), value => Assert.False(double.IsNaN(value))));
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepEveryHistoryLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveRangesAndLaggedExtremes()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.TrenderValues(effective, 3, 3, 2);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateTrender(length: 3); Assert.Equal(expected.Signals, data.SignalsList); using var context = new ComputeContext(); for (var slot = 0; slot < Keys.Length; slot++) { Assert.Equal(expected.Outputs[Keys[slot]], data.OutputValues[Keys[slot]]); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var fast = IndicatorCompute.ComputeTrenderFast(source, context, 3, series: Series[slot]); Assert.Equal(expected.Outputs[Keys[slot]], fast.ToArray()); }
    }
    [Fact]
    public void CustomAndLegacyAveragesKeepPriceAtrAdaptiveOrder()
    {
        var bars = new[] { 2d, 4, 2, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); var supplied = new[] { new[] { 10d, 20, 30, 40 }, new[] { 2d, 4, 6, 8 }, new[] { 11d, 19, 31, 39 } }; var inputs = new[] { new[] { 2d, 4, 2, 8 }, new[] { 2d, 3, 3, 7 }, new[] { 11d, 22, 27, 44 } }; var expected = BuiltInFormulaReferences.TrenderValues(bars, 2, 1, 2, supplied);
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(2, period); Assert.Equal(inputs[calls], values); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext();
            if (fast) { using var result = IndicatorCompute.ComputeTrenderFast(Data(bars), context, 2, maType: MovingAvgType.WeightedMovingAverage); Assert.Equal(expected.Outputs["Trender"], result.ToArray()); } else { var data = Data(bars).CalculateTrender(MovingAvgType.WeightedMovingAverage, 2); foreach (var key in Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); } Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var priceMean = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, bars.Select(b => b.Close).ToList()).ToArray(); var atr = Data(bars).CalculateAverageTrueRange(kind, 2).CustomValuesList.ToArray(); var adaptive = Enumerable.Range(0, bars.Length).Select(i => priceMean[i] + Math.Sign(bars[i].Close - (i == 0 ? 0 : bars[i - 1].Close)) * atr[i] / 2).ToList(); var smooth = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, adaptive).ToArray(); var reference = BuiltInFormulaReferences.TrenderValues(bars, 2, 1, 2, new[] { priceMean, atr, smooth }); var legacy = Data(bars).CalculateTrender(kind, 2); foreach (var key in Keys) Assert.Equal(reference.Outputs[key], legacy.OutputValues[key]);
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAveragesMomentsOrStops()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new TrenderState(length: 2); using var control = new TrenderState(length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var actual = state.Update(Native(bar), true, true); var expected = control.Update(Native(bar), true, true); foreach (var key in Keys) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void NonfiniteFactorsRejectBeforeMutatingSelectedInput()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        { var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateTrender(atrMult: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new TrenderState(atrMult: factor)); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeTrenderFast(data, context, atrMult: factor)); Assert.Equal(selected, data.CustomValuesList); }
    }
}
