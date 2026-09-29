using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KaseStopV1NumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(KaseDevStopV1)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRangeDeviationAndLevels(IndicatorValidationCase c, string route)
    {
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.KaseStopV1Outputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly string[] Keys = { "Dev1", "Dev2", "Dev3", "WarningLine" };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static Dictionary<string, double[]> Check(Bar[] bars, int fastLength = 2, int slowLength = 5, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, double[]? multiples = null)
    {
        var m = multiples ?? new[] { 0d, 1, 2.2, 3.6 }; var expected = BuiltInFormulaReferences.KaseStopV1Values(bars, fastLength, slowLength, length, Kind(kind), m); var batch = Data(bars).CalculateKaseDevStopV1(kind, fastLength, slowLength, length, m[0], m[1], m[2], m[3]); Assert.Equal(Keys, batch.OutputValues.Keys); Assert.Empty(batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        var options = new KaseDevStopV1SpecOptions(fastLength, slowLength, length, m[0], m[1], m[2], m[3], kind); using var context = new ComputeContext(); foreach (var key in Keys) { using var output = IndicatorCompute.ComputeKaseDevStopV1Fast(Data(bars), context, options, key); Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new KaseDevStopV1State(kind, fastLength, slowLength, length, m[0], m[1], m[2], m[3]);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 9d, -2, 3, 4, 1 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { 0d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs["Dev1"][i], point.Value); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], point.Outputs![key]); } }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideTypicalPricesRangesAndEveryLevelStayExact()
    {
        var gap = new[] { new Bar(DateTime.UnixEpoch, 10, 11, 9, 10, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 5, 6, 4, 5, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), -2, 1, -5, -2, 1) };
        foreach (var output in Check(gap, 1, 1, 1).Values) Assert.Equal(new[] { -1d, -1, -17 }, output);
        foreach (var length in new[] { 1, 3, 7 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 19).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), length, length + 1, length, kind, new[] { -2d, 0, .5, 3.6 });
    }
    [Fact]
    public void HandExamplesPreserveLagEqualityAndWarningCoefficientOrder()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); foreach (var output in Check(bars, 1, 2, 1).Values) Assert.Equal(new[] { -1d, -1, 0 }, output); foreach (var output in Check(bars, 1, 1, 1).Values) Assert.Equal(new[] { -1d, -1, 0 }, output);
        var results = Check(Bars(new[] { 0d, 2, 0, 2 }), 1, 2, 2, multiples: new[] { 0d, 1, 2, 3 }); var expected = new[] { new[] { 0d, 0, 2, 2 }, new[] { 0d, -1, 3, 2 }, new[] { 0d, -2, 4, 2 }, new[] { 0d, 1, 1, 2 } }; for (var slot = 0; slot < Keys.Length; slot++) Assert.Equal(expected[slot], results[Keys[slot]]);
        foreach (var output in Check(Bars(new[] { 8d, 0, 0, 0 }), 1, 1, 1).Values) Assert.Equal(new[] { 0d, 0, -8, 0 }, output);
    }
    [Fact]
    public void RecursiveTrendResidualSurvivesUntilPublishedMeansBecomeEqual()
    {
        var bars = Enumerable.Range(0, 101).Select(i => { var price = i == 0 ? 2d : 1; return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price + 1, price - 1, price, 1); }).ToArray(); var output = Check(bars, 2, 3, 1, MovingAvgType.ExponentialMovingAverage); Assert.Equal(3d, output["Dev1"][53]); Assert.Equal(-1d, output["Dev1"][54]);
    }
    [Fact]
    public void ExtendedRangesCancelAndZeroMultiplesRecoverAfterOverflow()
    {
        var max = double.MaxValue; foreach (var output in Check(Bars(new[] { -max, 0d, max, 0, 0, 0 }), 1, 1, 1, multiples: new double[4]).Values) Assert.Equal(new[] { double.NegativeInfinity, 0, -max, 0, -max, 0 }, output);
        Check(Bars(new[] { max, -max, 0d, max, 0 }), 1, 2, 2, multiples: new[] { 0d, .5, max, -max }); Check(Bars(new[] { double.Epsilon, 0d, 2 * double.Epsilon, 0 }), 1, 2, 2, multiples: new[] { 0d, 1, max, -max });
    }
    [Fact]
    public void EmptyAndExtremePeriodsKeepEveryHistoryLazy()
    {
        foreach (var length in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) { Check(Array.Empty<Bar>(), length, length, length, kind); Check(Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }), length, length, length, kind); }
    }
    [Fact]
    public void SelectedPricesPreserveEffectiveRangesAndNativeConsumerPolicy()
    {
        var bars = Enumerable.Range(0, 21).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 10 + i % 4, -3 - i % 3, 0, 1)).ToArray(); var prices = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2d) * 12).ToArray(); var multiples = new[] { 0d, 1, 2.2, 3.6 };
        var effective = bars.Select((b, i) => { var price = prices[i]; var previous = i == 0 ? price : prices[i - 1]; var inside = price >= b.Low && price <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(previous, price), inside ? b.Low : Math.Min(previous, price), price, b.Volume); }).ToArray(); var expected = BuiltInFormulaReferences.KaseStopV1Values(effective, 2, 5, 3, 1, multiples, selected: prices);
        var data = Data(bars); data.SetCustomValues(prices.ToList()); data.CalculateKaseDevStopV1(fastLength: 2, slowLength: 5, length: 3); Assert.Equal(expected.Signals, data.SignalsList); using var context = new ComputeContext(); var options = new KaseDevStopV1SpecOptions(2, 5, 3); foreach (var key in Keys) { Assert.Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(prices.ToList()); using var result = IndicatorCompute.ComputeKaseDevStopV1Fast(source, context, options, key); Assert.Equal(expected.Outputs[key], result.ToArray()); }
        using var state = new KaseDevStopV1State(fastLength: 2, slowLength: 5, length: 3); ((ICustomInputConsumer)state).ReadCloseAsInput(); for (var i = 0; i < effective.Length; i++) { var actual = state.Update(Native(effective[i]), true, true); foreach (var key in Keys) Assert.Equal(expected.Outputs[key][i], actual.Outputs![key]); }
    }
    [Fact]
    public void CustomAndLegacyAveragesKeepRangeSlowFastOrder()
    {
        var bars = new[] { 2d, 4, 6 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); var supplied = new[] { new[] { 10d, 20, 30 }, new[] { 1d, 1, 1 }, new[] { 0d, 2, 0 } }; var expected = new[] { 12d, -16, 36 };
        foreach (var fast in new[] { false, true })
        {
            var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(new[] { 1, 3, 2 }[calls], period); Assert.Equal(calls == 0 ? new[] { 3d, 5, 6 } : new[] { 2d, 4, 6 }, values); return supplied[calls++]; }; using var armed = ComponentAverage.Arm(new[] { callback, callback, callback }); using var context = new ComputeContext();
            if (fast) { using var output = IndicatorCompute.ComputeKaseDevStopV1Fast(Data(bars), context, new KaseDevStopV1SpecOptions(2, 3, 1), null); Assert.Equal(expected, output.ToArray()); } else { var data = Data(bars).CalculateKaseDevStopV1(fastLength: 2, slowLength: 3, length: 1); foreach (var output in data.OutputValues.Values) Assert.Equal(expected, output); } Assert.Equal(3, calls); Assert.Equal(3, ComponentAverage.Substitutions);
        }
        var kind = MovingAvgType.DoubleExponentialMovingAverage; var input = new[] { 2d, 4, 6 }; var external = new[] { CalculationsHelper.GetMovingAverageList(Data(bars), kind, 2, new List<double> { 3, 5, 6 }).ToArray(), SpreadAverage.Calculate(input, kind, 3).ToArray(), SpreadAverage.Calculate(input, kind, 2).ToArray() }; var reference = BuiltInFormulaReferences.KaseStopV1Values(bars, 2, 3, 2, 1, new[] { 0d, 1, 2.2, 3.6 }, external); var batch = Data(bars).CalculateKaseDevStopV1(kind, 2, 3, 2); using var ctx = new ComputeContext(); var options = new KaseDevStopV1SpecOptions(2, 3, 2, 0, 1, 2.2, 3.6, kind); foreach (var key in Keys) { using var result = IndicatorCompute.ComputeKaseDevStopV1Fast(Data(bars), ctx, options, key); Assert.Equal(reference.Outputs[key], batch.OutputValues[key]); Assert.Equal(reference.Outputs[key], result.ToArray()); }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceRangesMomentsOrTrendResidual()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new KaseDevStopV1State(fastLength: 2, slowLength: 3, length: 2); using var control = new KaseDevStopV1State(fastLength: 2, slowLength: 3, length: 2); foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var actual = state.Update(Native(bar), true, true); var expected = control.Update(Native(bar), true, true); foreach (var key in Keys) Assert.Equal(expected.Outputs![key], actual.Outputs![key]); }
        }
    }
    [Fact]
    public void NonfiniteMultiplesRejectBeforeMutatingSelectedPrices()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var slot in Enumerable.Range(0, 4))
        {
            var m = new double[4]; m[slot] = factor; var data = Data(Bars(new[] { 1d, 2, 3 })); var selected = new List<double> { 7, 8, 9 }; data.SetCustomValues(selected); Assert.Throws<ArgumentOutOfRangeException>(() => data.CalculateKaseDevStopV1(stdDev1: m[0], stdDev2: m[1], stdDev3: m[2], stdDev4: m[3])); Assert.Equal(selected, data.CustomValuesList); Assert.Throws<ArgumentOutOfRangeException>(() => new KaseDevStopV1State(stdDev1: m[0], stdDev2: m[1], stdDev3: m[2], stdDev4: m[3])); using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeKaseDevStopV1Fast(data, context, new KaseDevStopV1SpecOptions(2, 3, 2, m[0], m[1], m[2], m[3]), null)); Assert.Equal(selected, data.CustomValuesList);
        }
    }
}
