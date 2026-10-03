using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FastSlowKurtosisNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FastSlowKurtosisOscillator) || c.IndicatorType == typeof(FastandSlowKurtosisOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentLagDifferences(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    {
        var options = ((IBuiltInIndicator)indicator).CreateOptions();
        return options switch {
            FastSlowKurtosisOscillatorSpecOptions o => BuiltInFormulaReferences.FastSlowKurtosisOutputs(bars, o.Length),
            FastandSlowKurtosisOscillatorSpecOptions o => BuiltInFormulaReferences.FastSlowKurtosisOutputs(bars, o.Length, o.Ratio, Kind(o.MaType)),
            _ => throw new InvalidOperationException()
        };
    }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 3, double ratio = .03, MovingAvgType kind = MovingAvgType.WeightedMovingAverage)
    {
        var expected = BuiltInFormulaReferences.FastSlowKurtosisOutputs(bars, length, ratio, Kind(kind)); var batch = Data(bars).CalculateFastandSlowKurtosisOscillator(kind, length, ratio);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeFastAndSlowKurtosisFast(Data(bars), context, length, ratio, kind, key); Assert.Equal(expected[key], raw.ToArray()); }
        using var state = new FastandSlowKurtosisOscillatorState(kind, length, ratio);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Fsk"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void HandRecurrencePreservesUnclampedRatioAndStagedProducts()
    {
        var bars = new[] { 1d, 2, 4, 8, 16 }.Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.FastSlowKurtosisOutputs(bars, 1, .5); Assert.Equal(new[] { 0d, .5, .75, 1.375, 2.6875 }, expected["Fsk"]); Assert.Equal(expected["Fsk"], expected["Signal"]);
        foreach (var ratio in new[] { -.5, 0, .03, .5, 1, 1.5, double.Epsilon, double.MaxValue }) Check(bars, 1, ratio);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(bars, 3, .5, kind);
    }
    [Fact]
    public void OverflowRecoveryAndExtremePeriods()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 160).Select(i => Candle(i < 80 ? (i % 3 - 1) * scale : 1)).ToArray(); Check(bars); Check(bars, 1, .5);
        }
        var recovery = new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue }.Concat(Enumerable.Repeat(1d, 20)).Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.FastSlowKurtosisOutputs(recovery, 1, 1); Assert.True(double.IsPositiveInfinity(expected["Fsk"][1])); Assert.True(double.IsNegativeInfinity(expected["Fsk"][2])); Assert.Equal(0, expected["Fsk"][^1]); Check(recovery, 1, 1); Check(recovery, 2, 1); Check(recovery, 2, .5);
        Check(recovery, int.MaxValue); Check(Array.Empty<Bar>());
    }
    [Fact]
    public void SelectedPricesDriveDirectRawAndBothAliases()
    {
        var selected = Enumerable.Range(0, 80).Select(i => i % 5 == 0 ? double.MaxValue : i % 5 == 1 ? -double.MaxValue : 1 + i % 7).ToArray(); var bars = selected.Select(_ => Candle(0)).ToArray(); var expected = BuiltInFormulaReferences.FastSlowKurtosisOutputs(selected.Select(v => Candle(v)).ToArray());
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Contains(expected[key], value => value != 0);
            foreach (var options in new IIndicatorSpecOptions[] { new FastSlowKurtosisOscillatorSpecOptions(3), new FastandSlowKurtosisOscillatorSpecOptions() })
            { var data = Data(bars); data.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.FastandSlowKurtosisOscillator, options, key), context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray()); }
            var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var directRaw = IndicatorCompute.ComputeFastAndSlowKurtosisFast(direct, context, outputKey: key); Assert.Equal(expected[key], directRaw.ToArray());
        }
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); var result = batch.CalculateFastandSlowKurtosisOscillator(); foreach (var key in expected.Keys) Assert.Equal(expected[key], result.OutputValues[key]);
    }
    [Fact]
    public void DirectIndicatorConsumesSignalCallbackButComponentDoesNot()
    {
        var bars = Enumerable.Range(1, 8).Select(i => Candle(i)).ToArray(); var expected = BuiltInFormulaReferences.FastSlowKurtosisOutputs(bars)["Fsk"];
        foreach (var batch in new[] { false, true }) foreach (var key in new[] { "Fsk", "Signal" })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(3, period); Assert.Equal(expected, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var output = key == "Fsk" ? expected : Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Assert.Equal(output, Data(bars).CalculateFastandSlowKurtosisOscillator().OutputValues[key]);
            else { using var raw = IndicatorCompute.ComputeFastAndSlowKurtosisFast(Data(bars), context, outputKey: key); Assert.Equal(output, raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
        using (var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (_, _) => throw new InvalidOperationException("Component consumed a callback") }))
        {
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeFastAndSlowKurtosisFast(Data(bars), context); Assert.Equal(expected, raw.ToArray());
            using var alias = IndicatorCompute.ComputeFastSlowKurtosisOscillatorFast(Data(bars), context); Assert.Equal(expected, alias.ToArray()); Assert.Equal(0, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void CompositeConsumersKeepTheirOwnCallbackSequence()
    {
        var bars = Enumerable.Range(1, 12).Select(i => Candle(i, 20, 0)).ToArray();
        var line = BuiltInFormulaReferences.FastSlowKurtosisOutputs(bars)["Fsk"];
        foreach (var rsi in new[] { false, true })
        {
            var periods = new List<int>();
            var callbacks = Enumerable.Range(0, rsi ? 3 : 2).Select(_ => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((input, period) => {
                periods.Add(period); if (period == 6) Assert.Equal(line, input); return Enumerable.Repeat(2d, input.Count).ToArray();
            })).ToArray();
            using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
            using var raw = rsi ? IndicatorCompute.ComputeFastSlowRsiOscillatorFast(Data(bars), context) : IndicatorCompute.ComputeFastSlowStochasticOscillatorFast(Data(bars), context);
            Assert.Equal(rsi ? new[] { 9, 9, 6 } : new[] { 6, 9 }, periods); Assert.Equal(callbacks.Length, ComponentAverage.Substitutions);
            Assert.All(raw.ToArray(), v => Assert.Equal(rsi ? 20050d : 1002d, v));
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new FastandSlowKurtosisOscillatorState(); using var control = new FastandSlowKurtosisOscillatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
