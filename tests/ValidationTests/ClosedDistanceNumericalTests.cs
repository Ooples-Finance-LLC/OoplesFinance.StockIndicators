using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ClosedDistanceNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CFDV", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(ClosedFormDistanceVolatility)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentRootSearch(IndicatorValidationCase c, string route)
    {
        var indicator = (IBuiltInIndicator)c.Factory(); var options = indicator.CreateOptions(); var spec = new IndicatorSpec(indicator.BatchName, options);
        Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 })
        {
            var bars = Candles(Enumerable.Range(0, 23).Select(i => ((8 + i % 7) * scale, (i % 4) * scale, (4 + i % 3) * scale)).ToArray());
            var expected = BuiltInFormulaReferences.ClosedDistanceOutputs(bars, indicator)["Cfdv"];
            if (route == "batch") { Assert.Equal(expected, BuilderArmBinding.Compute(Data(bars), spec, target).ToArray()); continue; }
            if (route is "fast" or "arm")
            {
                using var context = new ComputeContext(); using var output = route == "arm" ? IndicatorCompute.ComputeArm(Data(bars), spec, context) : IndicatorCompute.TryComputeFast(Data(bars), spec, context);
                Assert.NotNull(output); Assert.Equal(expected, output.Value.ToArray()); continue;
            }
            var state = route == "native" ? StatefulIndicatorFactory.Create(spec) : StreamingIndicatorFactory.CreateState(spec); Assert.NotNull(state); using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(Candles((999, 0, 1))[0]), false, false);
                    foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Cfdv"]); }
                }
            }
        }
    }
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedSourcePreservesNonnegativeCandles(IndicatorValidationCase c)
    {
        var source = new Sma(3); var indicator = ((IndicatorBase)c.Factory()).Of(source);
        var bars = Candles(Enumerable.Range(0, 32).Select(i => (20d, 0d, 2d + i % 9)).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray(); var projected = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.ClosedDistanceOutputs(projected, (IBuiltInIndicator)indicator)["Cfdv"]; Assert.Equal(expected, run[indicator].ToArray());
        var feed = Bars.Live(); using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup().ConfigureIndicators(source, indicator).BuildAsync();
        foreach (var b in bars) feed.Publish(b); feed.Complete(); var actual = new List<double>(); await foreach (var point in live) actual.Add(point[indicator.Outputs[0]]); Assert.Equal(expected, actual);
    }
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static double[] Check(Bar[] bars, int length = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int referenceKind = 1)
    {
        var expected = BuiltInFormulaReferences.ClosedDistanceValues(bars, length, referenceKind); var values = expected.Outputs["Cfdv"];
        var batch = Data(bars).CalculateClosedFormDistanceVolatility(kind, length); Assert.Equal(values, batch.CustomValuesList); Assert.Equal(values, batch.OutputValues["Cfdv"]); Assert.Equal(expected.Signals, batch.SignalsList);
        using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeClosedFormDistanceVolatilityFast(Data(bars), context, length, kind); Assert.Equal(values, output.ToArray());
        using var state = new ClosedFormDistanceVolatilityState(kind, length); using var window = new ClosedFormDistanceWindow(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (7, 2, 5), (3, 0, 2), (9, 1, 4))) { state.Update(Native(b), true, false); window.Next(b.High, b.Low, b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, 0, -999))[0]), false, false); window.Next(999, 0, -999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.High, b.Low, b.Close, final);
                    Assert.Equal(values[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Cfdv"]); Assert.Equal(point.Value, direct.Value); Assert.Equal(expected.Signals[i], direct.Signal);
                }
            }
        }
        return values;
    }
    [Fact]
    public void WideSumsNearlyEqualBoundsAndTinyRatiosMatchExactRoots()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 }) foreach (var kind in Kinds)
            Check(Candles(Enumerable.Range(0, 15).Select(i => ((8 + i % 7) * scale, (1 + i % 4) * scale, (2 + i % 6) * scale)).ToArray()), 3, kind.Kind, kind.Reference);
        var next = Math.BitDecrement(double.MaxValue);
        var tight = Check(Candles((double.MaxValue, next, 0), (double.MaxValue, next, 1), (double.MaxValue, next, -1))); Assert.All(tight, v => Assert.True(v > 0 && v < 1e-15));
        Check(Candles((double.MaxValue, double.Epsilon, 0), (double.Epsilon, double.MaxValue, 1), (1, Math.BitDecrement(1d), 0)), 1);
        Check(Candles((1, 1e-20, 0), (1e-20, 1, 0), (double.Epsilon * 2, double.Epsilon, 0)), 1);
    }
    [Fact]
    public void HandZerosScaleSymmetryExpiryAndVolatilityGateArePreserved()
    {
        Assert.Equal(new[] { 0d, 1, 1, 0 }, Check(Candles((0, 0, 0), (1, 0, 1), (0, 1, 0), (3, 3, 3)), 1));
        var value = Check(Candles((9, 1, 5)), 1)[0]; Assert.InRange(value, .47476660661688, .47476660661690);
        Assert.Equal(value, Check(Candles((1, 9, 5)), 1)[0]); Assert.Equal(value, Check(Candles((18, 2, 10)), 1)[0]);
        var expiry = Check(Candles((double.MaxValue, 0, 1), (1, 1, 2), (1, 1, 3), (1, 1, 4)), 2); Assert.Equal(0, expiry[^1]);
        var bars = Candles((4, 0, 1), (4, 0, 2), (4, 0, -2), (4, 0, -1), (2, 2, 3), (2, 2, 4));
        Check(bars, 1, MovingAvgType.WeightedMovingAverage, 2); Check(bars, 3);
        using var window = new ClosedFormDistanceWindow(MovingAvgType.SimpleMovingAverage, 2);
        Assert.Equal(Signal.StrongBuy, window.Next(4, 0, 1, true).Signal); Assert.Equal(Signal.Buy, window.Next(4, 0, 2, true).Signal);
    }
    [Fact]
    public void ExtremePeriodsUseObservedHistoryOnly()
    {
        foreach (var kind in Kinds) foreach (var length in new[] { 0, int.MaxValue })
        { Check(Candles((double.MaxValue, 1, -double.MaxValue), (double.MaxValue, 1, double.MaxValue), (2, 1, 0), (0, 0, 1)), length, kind.Kind, kind.Reference); Check(Array.Empty<Bar>(), length, kind.Kind, kind.Reference); }
    }
    [Fact]
    public void SelectedRangesAndCustomMeanKeepCallbackAndSignals()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var selected = new[] { 5d, 12, 6, 15 }; var means = new[] { 0d, 20, -double.MaxValue, double.MaxValue };
        var effective = Candles((9, 1, 5), (12, 5, 12), (9, 1, 6), (15, 6, 15));
        foreach (var custom in new[] { false, true }) foreach (var fast in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.ClosedDistanceValues(effective, 3, 1, custom ? means : null); var calls = 0;
            Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(3, period); Assert.Equal(selected, values); calls++; return means; };
            using var armed = custom ? ComponentAverage.Arm(new[] { callback }) : null; using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
            if (fast) { using var output = IndicatorCompute.ComputeClosedFormDistanceVolatilityFast(data, context, 3, MovingAvgType.SimpleMovingAverage); Assert.Equal(expected.Outputs["Cfdv"], output.ToArray()); }
            else { data.CalculateClosedFormDistanceVolatility(MovingAvgType.SimpleMovingAverage, 3); Assert.Equal(expected.Outputs["Cfdv"], data.CustomValuesList); Assert.Equal(expected.Signals, data.SignalsList); }
            Assert.Equal(custom ? 1 : 0, calls);
        }
    }
    [Fact]
    public void NegativeRangesRejectBeforeCallbacksAndCannotAdvanceState()
    {
        foreach (var high in new[] { true, false }) foreach (var final in new[] { false, true })
        {
            var invalid = Candles((high ? -1 : 2, high ? 1 : -1, 0)); var calls = 0; Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (v, _) => { calls++; return v; };
            using var armed = ComponentAverage.Arm(new[] { callback }); using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(invalid).CalculateClosedFormDistanceVolatility(length: 2)); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeClosedFormDistanceVolatilityFast(Data(invalid), context, 2)); Assert.Equal(0, calls);
            using var state = new ClosedFormDistanceVolatilityState(length: 2); using var control = new ClosedFormDistanceVolatilityState(length: 2);
            var first = Native(Candles((4, 1, 2))[0]); state.Update(first, true, false); control.Update(first, true, false); Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(invalid[0]), final, false));
            foreach (var b in Candles((9, 2, 5), (3, 1, 2), (0, 0, 0))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
        using var extra = new ComputeContext(); var selected = Data(Candles((3, 1, 2))); selected.SetCustomValues(new List<double> { -1 }); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeClosedFormDistanceVolatilityFast(selected, extra));
    }
    [Fact]
    public void InvalidCandleFieldsCannotAdvanceNativeState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new ClosedFormDistanceVolatilityState(length: 2); using var control = new ClosedFormDistanceVolatilityState(length: 2); var initial = Native(Candles((4, 1, 2))[0]); state.Update(initial, true, false); control.Update(initial, true, false);
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((9, 2, 5), (2, 2, 2), (0, 0, 0))) Assert.Equal(control.Update(Native(b), true, false).Value, state.Update(Native(b), true, false).Value);
        }
    }
    [Fact]
    public void LegacyMeansRetainBatchAndNativeValues()
    {
        var bars = Candles((9, 1, 2), (4, 2, 3), (8, 3, 4), (6, 1, 2));
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        { var batch = Data(bars).CalculateClosedFormDistanceVolatility(kind, 3); using var state = new ClosedFormDistanceVolatilityState(kind, 3); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeClosedFormDistanceVolatilityFast(Data(bars), context, 3, kind); Assert.Equal(batch.CustomValuesList, output.ToArray()); for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.CustomValuesList[i], state.Update(Native(bars[i]), true, true).Value); }
    }
}
