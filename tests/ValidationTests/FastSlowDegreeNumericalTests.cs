using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FastSlowDegreeNumericalTests
{
    private static Bar[] Candles(params (double High, double Low, double Close)[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v.Close, v.High, v.Low, v.Close, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CSI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(FastSlowDegreeOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentDirectionalStages(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.FastSlowDegreeOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    private static (Dictionary<string, double[]> Outputs, Signal[] Signals) Check(double[] prices, int length = 7, int fast = 3, int slow = 2, int signalLength = 3, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int reference = 3)
    {
        var bars = Candles(prices.Select(v => (v, v, v)).ToArray()); var expected = BuiltInFormulaReferences.FastSlowDegreeValues(prices, length, fast, slow, signalLength, reference);
        var batch = Data(bars).CalculateFastSlowDegreeOscillator(kind, length, fast, slow, signalLength); Assert.Equal(expected.Outputs["Fsdo"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var (key, series) in new[] { ("Fsdo", IndicatorCompute.MacdSeries.Line), ("Signal", IndicatorCompute.MacdSeries.Signal), ("Histogram", IndicatorCompute.MacdSeries.Histogram) })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFastSlowDegreeOscillatorFast(Data(bars), context, length, fast, slow, signalLength, kind, series); Assert.Equal(expected.Outputs[key], output.ToArray()); }
        using var state = new FastSlowDegreeOscillatorState(kind, length, fast, slow, signalLength); using var window = new FastSlowDegreeWindow(kind, length, fast, slow, signalLength);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Candles((8, 1, 4), (3, -1, 2), (9, 3, 6), (7, -3, 1))) { state.Update(Native(b), true, false); window.Next(b.Close, true); } state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candles((999, -999, 999))[0]), false, false); window.Next(999, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true); var direct = window.Next(prices[i], final);
                    Assert.Equal(expected.Outputs["Fsdo"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Fsdo"]); Assert.Equal(point.Value, direct.Line); Assert.Equal(expected.Signals[i], direct.Trade);
                    Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]); Assert.Equal(point.Outputs["Signal"], direct.SignalLine); Assert.Equal(expected.Outputs["Histogram"][i], point.Outputs["Histogram"]); Assert.Equal(point.Outputs["Histogram"], direct.Histogram);
                }
            }
        }
        return (expected.Outputs, expected.Signals);
    }
    [Fact]
    public void HandPhaseWeightsLagAndPolynomialCancellation()
    {
        var result = Check(new[] { 2d, 4, 6, 8 }, 2, 1, 2, 2, MovingAvgType.SimpleMovingAverage, 1);
        Assert.Equal(new[] { 0d, -2, -2, -2 }, result.Outputs["Fsdo"]); Assert.Equal(new[] { 0d, -1, -2, -2 }, result.Outputs["Signal"]); Assert.Equal(new[] { 0d, -1, 0, 0 }, result.Outputs["Histogram"]);
        Assert.Equal(new[] { Signal.None, Signal.StrongSell, Signal.None, Signal.None }, result.Signals);
        var reverse = Check(new[] { 2d, 4, 6, 8 }, 2, 2, 1, 2, MovingAvgType.SimpleMovingAverage, 1); Assert.Equal(result.Outputs["Fsdo"].Select(v => -v), reverse.Outputs["Fsdo"]);
        foreach (var length in new[] { 1, 2, 100 }) { var equal = Check(new[] { double.MaxValue, -double.MaxValue, 1d, 2, 3 }, length, 3, 3); Assert.All(equal.Outputs.Values.SelectMany(v => v), v => Assert.Equal(0, v)); }
        var integerPhase = Check(new[] { double.MaxValue, -double.MaxValue, 1d, 2, 3 }, 1, 3, 2); Assert.All(integerPhase.Outputs.Values.SelectMany(v => v), v => Assert.Equal(0, v));
    }
    [Fact]
    public void IntegerPhaseReductionPreservesZerosQuadrantsAndSymmetry()
    {
        Assert.Equal(0, FastSlowDegreeWindow.SinePhase(0, 4)); Assert.Equal(1, FastSlowDegreeWindow.SinePhase(2, 4)); Assert.Equal(0, FastSlowDegreeWindow.SinePhase(4, 4)); Assert.Equal(-1, FastSlowDegreeWindow.SinePhase(6, 4));
        var turns = BigInteger.Pow(10, 50) * (2L * int.MaxValue); Assert.Equal(0, FastSlowDegreeWindow.SinePhase(turns + int.MaxValue, int.MaxValue));
        foreach (var length in new[] { 3, 17, int.MaxValue }) foreach (var phase in new[] { 1, 2, 13 })
        { Assert.Equal(FastSlowDegreeWindow.SinePhase(phase, length), FastSlowDegreeWindow.SinePhase((BigInteger)2 * length * BigInteger.Pow(10, 30) + phase, length)); Assert.Equal(-FastSlowDegreeWindow.SinePhase(phase, length), FastSlowDegreeWindow.SinePhase(-phase, length)); }
    }
    [Fact]
    public void WideProductsWindowExpiryAndSignalStagesMatchFractions()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 }) foreach (var kind in Kinds) foreach (var length in new[] { 2, 3, 7 })
            Check(Enumerable.Range(0, 19).Select(i => (i % 7 - 3) * scale).ToArray(), length, 5, 2, 3, kind.Kind, kind.Reference);
        Check(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, -1, 0, 1 }, 3, 5, 1, 2);
        Check(new[] { 1d, Math.BitIncrement(1d), Math.BitDecrement(1d), 1, 0, 1 }, 7, 2, 5, 3);
    }
    [Fact]
    public void ExtendedLineRetainsFiniteSignalAndHistogram()
    {
        var result = Check(Enumerable.Repeat(double.MaxValue, 4).ToArray(), 4, 4, 1, 4, MovingAvgType.SimpleMovingAverage, 1);
        Assert.True(double.IsPositiveInfinity(result.Outputs["Fsdo"][3])); Assert.True(double.IsFinite(result.Outputs["Signal"][3])); Assert.True(double.IsFinite(result.Outputs["Histogram"][3])); Assert.True(result.Outputs["Histogram"][3] > double.MaxValue / 2);
        var bars = Candles(Enumerable.Repeat((double.MaxValue, double.MaxValue, double.MaxValue), 4).ToArray()); var supplied = Enumerable.Repeat(double.MaxValue * .75, 4).ToArray();
        var expected = BuiltInFormulaReferences.FastSlowDegreeValues(bars.Select(b => b.Close).ToArray(), 4, 4, 1, 4, 1, supplied); Assert.True(double.IsFinite(expected.Outputs["Histogram"][3]));
        using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (_, _) => supplied }); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeFastSlowDegreeOscillatorFast(Data(bars), context, 4, 4, 1, 4, MovingAvgType.SimpleMovingAverage, IndicatorCompute.MacdSeries.Histogram); Assert.Equal(expected.Outputs["Histogram"], output.ToArray());
    }
    [Fact]
    public void ExhaustedSignalOverrideKeepsExtendedDefaultMean()
    {
        var bars = Candles(Enumerable.Repeat((double.MaxValue, double.MaxValue, double.MaxValue), 4).ToArray());
        foreach (var series in new[] { IndicatorCompute.MacdSeries.Signal, IndicatorCompute.MacdSeries.Histogram })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, _) => v });
            Assert.NotNull(ComponentAverage.Take(new[] { 0d }, 1)); // Consume the only binding before this indicator asks.
            using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeFastSlowDegreeOscillatorFast(Data(bars), context, 4, 4, 1, 1, MovingAvgType.SimpleMovingAverage, series);
            if (series == IndicatorCompute.MacdSeries.Histogram) Assert.All(output.ToArray(), value => Assert.Equal(0, value));
            else Assert.True(double.IsPositiveInfinity(output.ToArray()[3]));
            Assert.Equal(2, ComponentAverage.Requests); Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void EveryExtremePeriodGrowsOnlyWithObservations()
    {
        foreach (var kind in Kinds) foreach (var p in new[] { 0, int.MaxValue }) foreach (var slot in Enumerable.Range(0, 4))
        { var periods = new[] { 7, 3, 2, 3 }; periods[slot] = p; Check(new[] { 1d, -2, double.MaxValue, 0, -1 }, periods[0], periods[1], periods[2], periods[3], kind.Kind, kind.Reference); Check(Array.Empty<double>(), periods[0], periods[1], periods[2], periods[3], kind.Kind, kind.Reference); }
    }
    [Fact]
    public void SelectedPricesAndSingleSignalOverridePreserveAllOutputs()
    {
        var bars = Candles((9, 1, 4), (9, 1, 4), (9, 1, 4), (9, 1, 4)); var prices = new[] { 5d, 12, 6, 15 }; var supplied = new[] { 1d, 2, -3, 4 };
        foreach (var route in new[] { "batch", "Fsdo", "Signal", "Histogram" }) foreach (var custom in new[] { false, true })
        {
            var applied = custom && (route is "Signal" or "Histogram"); var expected = BuiltInFormulaReferences.FastSlowDegreeValues(prices, 7, 3, 2, 3, 3, applied ? supplied : null); var calls = 0;
            using var armed = custom ? ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (v, period) => { calls++; Assert.Equal(3, period); Assert.Equal(expected.Outputs["Fsdo"], v); return supplied; } }) : null;
            var data = Data(bars); data.SetCustomValues(prices.ToList()); using var context = new ComputeContext();
            if (route == "batch") { data.CalculateFastSlowDegreeOscillator(length: 7, signalLength: 3); foreach (var key in expected.Outputs.Keys) Assert.Equal(expected.Outputs[key], data.OutputValues[key]); Assert.Equal(expected.Signals, data.SignalsList); }
            else { var series = route == "Fsdo" ? IndicatorCompute.MacdSeries.Line : route == "Signal" ? IndicatorCompute.MacdSeries.Signal : IndicatorCompute.MacdSeries.Histogram; using var output = IndicatorCompute.ComputeFastSlowDegreeOscillatorFast(data, context, 7, signalLength: 3, series: series); Assert.Equal(expected.Outputs[route], output.ToArray()); }
            Assert.Equal(applied ? 1 : 0, calls);
        }
    }
    [Fact]
    public void LegacySignalAveragesPreserveAllThreeRoutes()
    {
        var prices = new[] { 2d, 3, 4, 2, -1, 0, 1, 7 }; var bars = Candles(prices.Select(v => (v, v, v)).ToArray());
        foreach (var kind in new[] { MovingAvgType.DoubleExponentialMovingAverage, MovingAvgType.TripleExponentialMovingAverage })
        {
            var batch = Data(bars).CalculateFastSlowDegreeOscillator(kind, 7, 3, 2, 3); using var state = new FastSlowDegreeOscillatorState(kind, 7, 3, 2, 3); using var context = new ComputeContext();
            foreach (var (key, series) in new[] { ("Fsdo", IndicatorCompute.MacdSeries.Line), ("Signal", IndicatorCompute.MacdSeries.Signal), ("Histogram", IndicatorCompute.MacdSeries.Histogram) }) { using var output = IndicatorCompute.ComputeFastSlowDegreeOscillatorFast(Data(bars), context, 7, signalLength: 3, maType: kind, series: series); Assert.Equal(batch.OutputValues[key], output.ToArray()); }
            for (var i = 0; i < bars.Length; i++) { var point = state.Update(Native(bars[i]), true, true); foreach (var key in batch.OutputValues.Keys) Assert.Equal(batch.OutputValues[key][i], point.Outputs![key]); }
        }
    }
    [Fact]
    public void InvalidCandleCannotAdvanceDirectionalOrSignalState()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var field in Enumerable.Range(0, 5)) foreach (var final in new[] { false, true })
        {
            using var state = new FastSlowDegreeOscillatorState(length: 3); using var control = new FastSlowDegreeOscillatorState(length: 3);
            foreach (var b in Candles((4, 1, 2), (9, -2, 5))) { state.Update(Native(b), true, false); control.Update(Native(b), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = bad; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), final, true));
            foreach (var b in Candles((1, -2, 0), (2, 0, 1), (0, 0, 0))) { var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["Signal"], actual.Outputs!["Signal"]); }
        }
    }
}
