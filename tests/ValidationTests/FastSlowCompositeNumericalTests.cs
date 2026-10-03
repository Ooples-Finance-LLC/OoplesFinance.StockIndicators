using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FastSlowCompositeNumericalTests
{
    private static Bar[] Bars(IEnumerable<double> prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("FSC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FastSlowRsiOscillator) || c.IndicatorType == typeof(FastSlowStochasticOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentMomentumComposite(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FastSlowCompositeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    private static StockData Batch(StockData data, bool rsi, MovingAvgType kind, int a, int b, int c, int d) => rsi ? data.CalculateFastandSlowRelativeStrengthIndexOscillator(kind, a, b, c, d) : data.CalculateFastandSlowStochasticOscillator(kind, a, b, c, d);
    private static IStreamingIndicatorState State(bool rsi, MovingAvgType kind, int a, int b, int c, int d) => rsi ? new FastandSlowRelativeStrengthIndexOscillatorState(kind, a, b, c, d) : new FastandSlowStochasticOscillatorState(kind, a, b, c, d);
    private static Dictionary<string, double[]> Check(Bar[] bars, bool rsi, int a = 2, int b = 4, int c = 3, int d = 5, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var expected = BuiltInFormulaReferences.FastSlowCompositeValues(bars, rsi, a, b, c, d, Kind(kind)); var batch = Batch(Data(bars), rsi, kind, a, b, c, d); var key = rsi ? "Fsrsi" : "Fsst"; Assert.Equal(expected.Signals, batch.SignalsList); Assert.Equal(expected.Outputs[key], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var outputKey in new[] { key, "Signal" }) { Assert.Equal(expected.Outputs[outputKey], batch.OutputValues[outputKey]); using var output = IndicatorCompute.ComputeFastSlowCompositeFast(Data(bars), context, rsi, kind, a, b, c, d, outputKey); Assert.Equal(expected.Outputs[outputKey], output.ToArray()); }
        var state = State(rsi, kind, a, b, c, d); using var lifetime = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var bar in Bars(new[] { 8d, -3, 4, 1, 7 })) state.Update(Native(bar), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++) { state.Update(Native(Bars(new[] { -99d })[0]), false, false); foreach (var final in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), final, true); Assert.Equal(expected.Outputs[key][i], point.Value); foreach (var outputKey in new[] { key, "Signal" }) Assert.Equal(expected.Outputs[outputKey][i], point.Outputs![outputKey]); } }
        }
        return expected.Outputs;
    }
    [Fact]
    public void WideMomentumBothLevelsAndSignalMeansMatchRationalStages()
    {
        using var preview = new FastSlowCompositeWindow(false, MovingAvgType.ExponentialMovingAverage, 1, 1, 3, 1);
        Assert.Equal(0, preview.Next(0, 0, 0, true, externalVelocity: 0).Line);
        Assert.Equal(50, preview.Next(10, 10, 10, true, externalVelocity: 0).Line);
        Assert.Equal(35, preview.Next(100, 5, 5, false, externalVelocity: 0).Line);
        Assert.Equal(50, preview.Next(5, 5, 5, false, externalVelocity: 0).Line);
        Assert.Equal(50, preview.Next(5, 5, 5, true, externalVelocity: 0).Line);
        foreach (var rsi in new[] { false, true }) foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 19).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 7 - 3) * scale, 1)).ToArray(), rsi, kind: kind);
    }
    [Fact]
    public void HandSeedsMomentumAndCompositeScalesArePreserved()
    {
        var flat = Enumerable.Range(0, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 2, 3, 1, 2, 1)).ToArray(); var rsi = Check(flat, true, 3, 2, 3, 2); Assert.Equal(new[] { 100d, 100, 100 }, rsi["Fsrsi"]); Assert.Equal(new[] { 200d / 3, 100, 100 }, rsi["Signal"]);
        var stochastic = Check(flat, false, 3, 2, 3, 2); Assert.Equal(new[] { 25d, 125d / 3, 50 }, stochastic["Fsst"]);
        var pulse = new[] { 0d, 1, 1 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray(); Assert.Equal(new[] { 100d, 400 }, Check(pulse, true, 1, 1, 1, 1)["Fsrsi"].Take(2)); Assert.Equal(new[] { 50d, 65 }, Check(pulse, false, 1, 1, 1, 1)["Fsst"].Take(2));
    }
    [Fact]
    public void ExtendedCompositesAndSignalHistoryRecoverFromOverflow()
    {
        var max = double.MaxValue / 16; var prices = new[] { max, -max, max }.Concat(Enumerable.Repeat(0d, 180)).ToArray();
        foreach (var rsi in new[] { false, true }) { var output = Check(Bars(prices), rsi, 1, 1, 2, 2); var key = rsi ? "Fsrsi" : "Fsst"; Assert.Contains(output[key], double.IsInfinity); Assert.All(output.SelectMany(v => v.Value), value => Assert.False(double.IsNaN(value))); Assert.True(double.IsFinite(output[key][^1])); Assert.True(double.IsFinite(output["Signal"][^1])); }
        foreach (var rsi in new[] { false, true }) Check(new[] { -double.MaxValue, 0d, double.MaxValue, 0, -double.MaxValue }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, double.MaxValue, -double.MaxValue, v, 1)).ToArray(), rsi, 1, 2, 2, 3);
    }
    [Fact]
    public void FlatRsiCarryAndStochasticExtremaExpiryKeepTheirOwnInputs()
    {
        using var carry = new FastSlowCompositeWindow(true, MovingAvgType.WildersSmoothingMethod, 1, 1, 3, 1);
        foreach (var price in new[] { 0d, 0, 2, 0, 1 }) carry.Next(price, price, price, true, externalVelocity: 0);
        Assert.Equal(58.62068965517241, carry.Next(1, 1, 1, false, externalVelocity: 0).Line);
        Assert.Equal(58.62068965517241, carry.Next(1, 1, 1, true, externalVelocity: 0).Line);
        foreach (var kind in new[] { MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(Bars(new[] { 0d, 3, 1, 4, 2 }.Concat(Enumerable.Repeat(2d, 160))), true, 2, 4, 3, 5, kind);
        Check(Bars(new[] { 0d, 100, 1, 2, 3, 0, -1, 4 }), false, 2, 4, 2, 3);
        foreach (var rsi in new[] { false, true }) Check(Bars(new[] { 0d, 3, 1, 4, 2, 0, 3, -1 }), rsi, 1, 1, 1, 1);
    }
    [Fact]
    public void EveryExtremePeriodKeepsMomentumAndMeanHistoryLazy()
    {
        foreach (var rsi in new[] { false, true }) foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) foreach (var kind in Kinds) foreach (var position in Enumerable.Range(0, 4)) { var lengths = new[] { 2, 4, 3, 5 }; lengths[position] = period; foreach (var bars in new[] { Array.Empty<Bar>(), Bars(new[] { 1d, 3, -2, 5, 0, 2, 4 }) }) Check(bars, rsi, lengths[0], lengths[1], lengths[2], lengths[3], kind); }
    }
    [Fact]
    public void CustomCallbacksKeepPrimaryArityAndSignalOutputOrder()
    {
        var bars = new[] { 2d, 4, 6, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, 10, -10, v, 1)).ToArray(); var selected = new[] { -2d, 0, 4, 3 }; var velocity = new[] { .1, -.2, .3, 0 }; var signal = new[] { 7d, 8, 9, 10 }; var rawVelocity = BuiltInFormulaReferences.FastSlowKurtosisOutputs(Bars(selected), 2)["Fsk"];
        foreach (var rsi in new[] { false, true })
        {
            var key = rsi ? "Fsrsi" : "Fsst"; var level = rsi ? new[] { 50d, 25, 50, 25 } : new[] { 10d, 20, 30, 40 }; var expected = BuiltInFormulaReferences.FastSlowCompositeValues(bars, rsi, 2, 4, 3, 5, 2, new[] { velocity, level, signal });
            foreach (var route in new[] { "batch", key, "Signal" })
            {
                var supplied = rsi ? new[] { new[] { 1d, 1, 1, 1 }, new[] { 1d, 3, 1, 3 }, velocity, signal } : new[] { velocity, level, signal }; var periods = rsi ? new[] { 3, 3, 4, 5 } : new[] { 4, 3, 5 }; var primaryCount = rsi ? 3 : 2; var count = route == key ? primaryCount : primaryCount + 1; var calls = 0;
                Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) => { Assert.Equal(periods[calls], period); var input = calls == primaryCount ? expected.Outputs[key] : rsi ? calls == 0 ? new[] { 0d, 2, 4, 0 } : calls == 1 ? new[] { 0d, 0, 0, 1 } : rawVelocity : calls == 0 ? rawVelocity : new[] { 40d, 50, 70, 65 }; Assert.Equal(input, values); return supplied[calls++]; };
                using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback, count).ToArray()); using var context = new ComputeContext(); var data = Data(bars); data.SetCustomValues(selected.ToList());
                if (route == "batch") { Batch(data, rsi, MovingAvgType.WeightedMovingAverage, 2, 4, 3, 5); foreach (var outputKey in new[] { key, "Signal" }) Assert.Equal(expected.Outputs[outputKey], data.OutputValues[outputKey]); Assert.Equal(expected.Signals, data.SignalsList); }
                else { using var output = IndicatorCompute.ComputeFastSlowCompositeFast(data, context, rsi, MovingAvgType.WeightedMovingAverage, 2, 4, 3, 5, route); Assert.Equal(expected.Outputs[route], output.ToArray()); } Assert.Equal(count, calls); Assert.Equal(count, ComponentAverage.Requests); Assert.Equal(count, ComponentAverage.Substitutions);
            }
        }
    }
    [Fact]
    public void SelectedRangesAndLegacyAveragesAgreeAcrossOutputs()
    {
        var bars = new[] { 2d, 4, 6, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, 10, -10, v, 1)).ToArray(); var selected = new[] { 30d, -40, 4, 20 }; var effective = bars.Select((b, i) => { var p = selected[i]; var previous = i == 0 ? p : selected[i - 1]; var inside = p >= b.Low && p <= b.High; return new Bar(b.Time, b.Open, inside ? b.High : Math.Max(p, previous), inside ? b.Low : Math.Min(p, previous), p, b.Volume); }).ToArray(); using var context = new ComputeContext();
        foreach (var rsi in new[] { false, true })
        {
            var key = rsi ? "Fsrsi" : "Fsst"; var expected = BuiltInFormulaReferences.FastSlowCompositeValues(effective, rsi, 2, 4, 3, 5, 2); var data = Data(bars); data.SetCustomValues(selected.ToList()); Batch(data, rsi, MovingAvgType.WeightedMovingAverage, 2, 4, 3, 5); Assert.Equal(expected.Signals, data.SignalsList); var state = State(rsi, MovingAvgType.WeightedMovingAverage, 2, 4, 3, 5); using var lifetime = (IDisposable)state;
            foreach (var outputKey in new[] { key, "Signal" }) { Assert.Equal(expected.Outputs[outputKey], data.OutputValues[outputKey]); var source = Data(bars); source.SetCustomValues(selected.ToList()); using var output = IndicatorCompute.ComputeFastSlowCompositeFast(source, context, rsi, MovingAvgType.WeightedMovingAverage, 2, 4, 3, 5, outputKey); Assert.Equal(expected.Outputs[outputKey], output.ToArray()); } for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(effective[i]), true, true); foreach (var outputKey in new[] { key, "Signal" }) Assert.Equal(expected.Outputs[outputKey][i], point.Outputs![outputKey]); }
            var kind = MovingAvgType.DoubleExponentialMovingAverage; var legacy = Batch(Data(bars), rsi, kind, 2, 4, 3, 5); var native = State(rsi, kind, 2, 4, 3, 5); using var legacyLifetime = (IDisposable)native; foreach (var outputKey in new[] { key, "Signal" }) { using var output = IndicatorCompute.ComputeFastSlowCompositeFast(Data(bars), context, rsi, kind, 2, 4, 3, 5, outputKey); Assert.Equal(legacy.OutputValues[outputKey], output.ToArray()); } for (var i = 0; i < bars.Length; i++) { var point = native.Update(Native(bars[i]), true, true); foreach (var outputKey in new[] { key, "Signal" }) Assert.Equal(legacy.OutputValues[outputKey][i], point.Outputs![outputKey]); }
        }
    }
    [Fact]
    public void ObsoleteAliasLengthDoesNotChangeFixedDefaultsOrSignalPeriod()
    {
        var bars = Bars(Enumerable.Range(0, 48).Select(i => (double)(i % 7 - i % 3))); using var context = new ComputeContext();
        foreach (var rsi in new[] { false, true }) foreach (var period in new[] { 1, 7, int.MaxValue })
        {
            var name = rsi ? IndicatorName.FastandSlowRelativeStrengthIndexOscillator : IndicatorName.FastandSlowStochasticOscillator; var key = rsi ? "Fsrsi" : "Fsst"; IIndicatorSpecOptions options = rsi ? new FastSlowRsiOscillatorSpecOptions(period) : new FastSlowStochasticOscillatorSpecOptions(period); var expected = BuiltInFormulaReferences.FastSlowCompositeValues(bars, rsi, 3, 6, 9, rsi ? 6 : 9, 2).Outputs;
            foreach (var outputKey in new[] { key, "Signal" }) { using var output = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(name, options, outputKey), context); Assert.NotNull(output); Assert.Equal(expected[outputKey], output.Value.ToArray()); }
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMomentumLevelsOrSignal()
    {
        foreach (var rsi in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = State(rsi, MovingAvgType.WeightedMovingAverage, 2, 4, 3, 5); var control = State(rsi, MovingAvgType.WeightedMovingAverage, 2, 4, 3, 5); using var first = (IDisposable)state; using var second = (IDisposable)control; foreach (var bar in Bars(new[] { 1d, 3, -2, 5 })) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true)); foreach (var bar in Bars(new[] { 3d, 8, -2, 5, 0 })) { var a = state.Update(Native(bar), true, true); var b = control.Update(Native(bar), true, true); Assert.Equal(b.Value, a.Value); Assert.Equal(b.Outputs!["Signal"], a.Outputs!["Signal"]); }
        }
    }
}
