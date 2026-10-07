using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KaseStopV2NumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KaseDevStopV2)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeDeviationAndLevels(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KaseStopV2Outputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly string[] Keys = { "Dev1", "Dev2", "Dev3", "Dev4" };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static Dictionary<string, double[]> Check(Bar[] bars, int fastLength = 2, int slowLength = 5, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, double[]? multiples = null)
    {
        var m = multiples ?? new[] { 0d, 1, 2.2, 3.6 }; var expected = BuiltInFormulaReferences.KaseStopV2Values(bars, fastLength, slowLength, length, Kind(kind), m); var batch = Data(bars).CalculateKaseDevStopV2(kind, fastLength, slowLength, length, m[0], m[1], m[2], m[3]); Assert.Equal(Keys, batch.OutputValues.Keys); Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); for (var slot = 0; slot < Keys.Length; slot++) { using var output = IndicatorCompute.ComputeKaseDevStopV2Fast(Data(bars), context, fastLength, slowLength, length, m[slot], kind); Assert.Equal(expected.Outputs[Keys[slot]], batch.OutputValues[Keys[slot]]); Assert.Equal(expected.Outputs[Keys[slot]], output.ToArray()); }
        using var state = new KaseDevStopV2State(kind, fastLength, slowLength, length, m[0], m[1], m[2], m[3]);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Dev1"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideRangesPopulationDeviationAndEveryProjectionRemainExact()
    {
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 19).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, length + 1, length, kind, new[] { -2d, 0, .5, 3.6 });
    }
    [Fact]
    public void HandExamplesPreserveTwoBarLagPopulationDivisorAndEqualitySide()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); foreach (var output in Check(bars, 1, 2, 1).Values) Assert.Equal(new[] { 0d, 0, 2 }, output); foreach (var output in Check(bars, 1, 1, 1).Values) Assert.Equal(new[] { 4d, 8, 10 }, output);
        var results = Check(Bars(new[] { 0d, 2, 0, 2 }), 1, 2, 2, multiples: new[] { 0d, 1, 2, 3 }); for (var slot = 0; slot < Keys.Length; slot++) Assert.Equal(new[] { 0d, 1d - slot, 2, 0 }, results[Keys[slot]]);
        foreach (var output in Check(Bars(new[] { 8d, 0, 0, 0 }), 1, 1, 1).Values) Assert.Equal(new[] { 16d, 8, 8, 0 }, output);
    }
    [Fact]
    public void ExtendedRangesCancelAndZeroMultiplesRecoverAfterOverflow()
    {
        var max = double.MaxValue; var bars = new[] { new Bar(DateTime.UnixEpoch, 0, max, -max, 0, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, 0, 0, 0, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 0, 0, 0, 0, 1), new Bar(DateTime.UnixEpoch.AddMinutes(3), 0, 0, 0, 0, 1) };
        foreach (var output in Check(bars, 1, 1, 1, multiples: new double[4]).Values) Assert.Equal(new[] { max, double.PositiveInfinity, 0, 0 }, output);
        Check(bars, 1, 2, 2, multiples: new[] { 0d, .5, double.MaxValue, -double.MaxValue }); Check(Bars(new[] { double.Epsilon, 0d, 2 * double.Epsilon, 0 }), 1, 2, 2, multiples: new[] { 0d, 1, double.MaxValue, -double.MaxValue });
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepEveryHistoryLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, length, length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, length, length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveRangesAndAllOutputLevels()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray(); var multiples = new[] { 0d, 1, 2.2, 3.6 };
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.KaseStopV2Values(effective, 2, 5, 3, 1, multiples);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateKaseDevStopV2(fastLength: 2, slowLength: 5, length: 3); Assert.Equal(expected.Signals, data.SignalsList); using var context = new ComputeContext(); for (var slot = 0; slot < 4; slot++) { Assert.Equal(expected.Outputs[Keys[slot]], data.OutputValues[Keys[slot]]); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var result = IndicatorCompute.ComputeKaseDevStopV2Fast(source, context, 2, 5, 3, multiples[slot]); Assert.Equal(expected.Outputs[Keys[slot]], result.ToArray()); }
    }
    [Fact]
    public void CustomAndLegacyAveragesKeepFastSlowRangeOrder()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); var supplied = new[] { new[] { 1d, 1, 1 }, new[] { 0d, 2, 0 }, new[] { 10d, 20, 30 } }; var expected = new[] { -7d, 23, -23 };
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(new[] { 2, 3, 1 }[calls], period); Assert.Equal(calls == 2 ? new[] { 3d, 5, 5 } : new[] { 2d, 4, 6 }, values); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeKaseDevStopV2Fast(Data(bars), context, 2, 3, 1); Assert.Equal(expected, output.ToArray()); } else { var data = Data(bars).CalculateKaseDevStopV2(fastLength: 2, slowLength: 3, length: 1); foreach (var output in data.OutputValues.Values) Assert.Equal(expected, output); } Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; double[] Average(double[] values, int period) => CalculationsHelper.GetMovingAverageList(Data(bars), kind, period, values.ToList()).ToArray(); var external = new[] { Average(new[] { 2d, 4, 6 }, 2), Average(new[] { 2d, 4, 6 }, 3), Average(new[] { 3d, 5, 5 }, 2) }; var reference = BuiltInFormulaReferences.KaseStopV2Values(bars, 2, 3, 2, 1, new[] { 0d, 1, 2.2, 3.6 }, external); var batch = Data(bars).CalculateKaseDevStopV2(kind, 2, 3, 2); using var ctx = new ComputeContext(); for (var i = 0; i < 4; i++) { using var result = IndicatorCompute.ComputeKaseDevStopV2Fast(Data(bars), ctx, 2, 3, 2, new[] { 0d, 1, 2.2, 3.6 }[i], kind); Assert.Equal(reference.Outputs[Keys[i]], batch.OutputValues[Keys[i]]); Assert.Equal(reference.Outputs[Keys[i]], result.ToArray()); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRangesMomentsOrAverages()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new KaseDevStopV2State(fastLength: 2, slowLength: 3, length: 2); using var control = new KaseDevStopV2State(fastLength: 2, slowLength: 3, length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var actual = state.Update(Native(bar), true, true); var expected = control.Update(Native(bar), true, true); foreach (var key in Keys) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void NonfiniteMultiplesRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var slot in Enumerable.Range(0, 4))
        {
            var m = new double[4]; m[slot] = factor; var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateKaseDevStopV2(stdDev1: m[0], stdDev2: m[1], stdDev3: m[2], stdDev4: m[3])); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new KaseDevStopV2State(stdDev1: m[0], stdDev2: m[1], stdDev3: m[2], stdDev4: m[3])); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeKaseDevStopV2Fast(data, context, stdDev: factor)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
