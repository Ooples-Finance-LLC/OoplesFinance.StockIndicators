using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UtBotNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(UtBotAlerts)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentStopPositionAndFlags(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.UtBotOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly string[] Keys = { "TrailingStop", "Position", "Buy", "Sell" };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static Dictionary<string, double[]> Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.WildersSmoothingMethod, double factor = 1)
    {
        var expected = BuiltInFormulaReferences.UtBotValues(bars, length, Kind(kind), factor); var batch = Data(bars).CalculateUtBotAlerts(kind, length, factor); Assert.Equal(Keys, batch.OutputValues.Keys); Assert.Equal(expected.Outputs["TrailingStop"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); for (var slot = 0; slot < Keys.Length; slot++) { using var fast = IndicatorCompute.ComputeUtBotAlertsFast(Data(bars), context, kind, length, factor, slot); Assert.Equal(expected.Outputs[Keys[slot]], batch.OutputValues[Keys[slot]]); Assert.Equal(expected.Outputs[Keys[slot]], fast.ToArray()); }
        using var state = new UtBotAlertsState(kind, length, factor);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["TrailingStop"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideRangesAndBothGapDirectionsPreserveEveryOutput()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 }) foreach (var factor in new[] { -2d, 0, .5, 2 })
            Check(Enumerable.Range(0, 17).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, kind, factor);
        var gap = new[] { new Bar(DateTime.UnixEpoch, 15, 16, 14, 15, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 5, 8, 2, 5, 1) }; Assert.Equal(new[] { 13d, 18 }, Check(gap, 1)["TrailingStop"]);
        var reversed = gap.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume)).ToArray(); Assert.Equal(new[] { -13d, -18 }, Check(reversed, 1)["TrailingStop"]);
    }
    [Fact]
    public void ExtendedRangesCancelBeforePublicationAndZeroFactorsStayFinite()
    {
        var prices = new[] { 0d, -double.MaxValue, double.MaxValue }; var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, p, 1)).ToArray(); var output = Check(bars, 1, factor: .5); Assert.Equal(new[] { double.MaxValue, 0, 0 }, output["TrailingStop"]); Assert.Equal(new[] { 0d, 0, 1 }, output["Position"]);
        output = Check(bars, 1, factor: 0); Assert.Equal(prices, output["TrailingStop"]); foreach (var key in Keys.Skip(1)) Assert.All(output[key], v => Assert.Equal(0d, v)); Check(bars, 2, factor: double.MaxValue); Check(Bars(new[] { double.Epsilon, 2 * double.Epsilon, 0d, double.Epsilon }), 2, factor: double.MaxValue);
    }
    [Fact]
    public void HandCrossingsKeepPositionStrictButAlertEqualityInclusive()
    {
        var output = Check(Bars(new[] { 80d, 100, 50, 120 }), 1); Assert.Equal(new[] { 80d, 80, 100, 50 }, output["TrailingStop"]); Assert.Equal(new[] { 0d, 0, -1, 1 }, output["Position"]); Assert.Equal(new[] { 0d, 1, 0, 1 }, output["Buy"]); Assert.Equal(new[] { 0d, 0, 1, 0 }, output["Sell"]);
        output = Check(Bars(new[] { -80d, -100, -50, -120 }), 1); Assert.Equal(new[] { -80d, -80, -100, -50 }, output["TrailingStop"]); Assert.Equal(new[] { 0d, 1, 0, 1 }, output["Sell"]); Assert.Equal(new[] { 0d, 0, 1, 0 }, output["Buy"]);
        var zero = new[] { new Bar(DateTime.UnixEpoch, 0, 1, -1, 0, 1) }; foreach (var factor in new[] { -1d, 1 }) { output = Check(zero, 1, factor: factor); Assert.Equal(2 * factor, output["TrailingStop"][0]); foreach (var key in Keys.Skip(1)) Assert.Equal(0d, output[key][0]); }
        var first = new[] { new Bar(DateTime.UnixEpoch, 15, 16, 14, 15, 1) }; Check(first, 1); Assert.Equal(Signal.Buy, Data(first).CalculateUtBotAlerts(length: 1).SignalsList[0]);
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepLazyAverageHistory()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveRangesAndAllFourFastSlots()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray();
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.UtBotValues(effective, 3, 6, 1);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateUtBotAlerts(length: 3); Assert.Equal(expected.Signals, data.SignalsList); using var context = new ComputeContext();
        for (var slot = 0; slot < Keys.Length; slot++) { Assert.Equal(expected.Outputs[Keys[slot]], data.OutputValues[Keys[slot]]); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var result = IndicatorCompute.ComputeUtBotAlertsFast(source, context, length: 3, slot: slot); Assert.Equal(expected.Outputs[Keys[slot]], result.ToArray()); }
    }
    [Fact]
    public void CustomAtrAndLegacyAveragesReachEveryProjection()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 5, 10, 0, 5, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 4, 8, 2, 4, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 5, 6, 3, 5, 1) }; var target = new[] { new[] { 4d, 6, 6 }, new double[3], new double[3], new[] { 0d, 1, 0 } };
        foreach (var slot in new[] { -1, 0, 1, 2, 3 })
        {
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 10d, 6, 3 }, values); return new[] { 1d, 2, 3 }; });
            if (slot < 0) { var data = Data(bars).CalculateUtBotAlerts(length: 2); for (var i = 0; i < Keys.Length; i++) Assert.Equal(target[i], data.OutputValues[Keys[i]]); Assert.Equal(target[0], data.CustomValuesList); }
            else { using var context = new ComputeContext(); using var result = IndicatorCompute.ComputeUtBotAlertsFast(Data(bars), context, length: 2, slot: slot); Assert.Equal(target[slot], result.ToArray()); } Assert.Equal(1, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var atr = Data(bars).CalculateAverageTrueRange(kind, 2).CustomValuesList; var priorStop = 0d; var expected = new double[bars.Length]; for (var i = 0; i < bars.Length; i++) { var price = bars[i].Close; var previous = bars[i == 0 ? 0 : i - 1].Close; var candidate = price > priorStop ? price - atr[i] : price + atr[i]; expected[i] = price > priorStop && previous > priorStop ? Math.Max(priorStop, candidate) : price < priorStop && previous < priorStop ? Math.Min(priorStop, candidate) : candidate; priorStop = expected[i]; }
        var batch = Data(bars).CalculateUtBotAlerts(kind, 2); using var ctx = new ComputeContext(); using var fast = IndicatorCompute.ComputeUtBotAlertsFast(Data(bars), ctx, kind, 2); for (var i = 0; i < bars.Length; i++) { Assert.Equal(expected[i], batch.CustomValuesList[i], 12); Assert.Equal(batch.CustomValuesList[i], fast.Span[i]); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceAverageStopOrPosition()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new UtBotAlertsState(length: 2); using var control = new UtBotAlertsState(length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); foreach (var key in Keys) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void NonfiniteFactorsRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateUtBotAlerts(keyValue: factor)); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new UtBotAlertsState(keyValue: factor)); using var context = new ComputeContext(); foreach (var slot in Enumerable.Range(0, 4)) Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeUtBotAlertsFast(data, context, factor: factor, slot: slot)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
