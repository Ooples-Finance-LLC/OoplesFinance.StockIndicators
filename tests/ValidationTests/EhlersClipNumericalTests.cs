using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EhlersClipNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSimpleClipIndicator)).Select(c => new object[] { c });
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
    { var o = (EhlersSimpleClipIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.EhlersClipOutputs(bars, o.Length1, o.Length3, o.SignalLength, Kind(o.MaType)); }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int lag = 2, int length = 50, int smoothing = 22, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int unused = 10)
    {
        var expected = BuiltInFormulaReferences.EhlersClipOutputs(bars, lag, length, smoothing, Kind(kind)); var batch = Data(bars).CalculateEhlersSimpleClipIndicator(kind, lag, unused, length, smoothing);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeEhlersSimpleClipIndicatorFast(Data(bars), context, lag, length, smoothing, kind, key); Assert.Equal(expected[key], raw.ToArray()); }
        using var state = new EhlersSimpleClipIndicatorState(kind, lag, unused, length, smoothing);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Esci"][i], actual.Value); Assert.InRange(actual.Value, -4d, 4d); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void HandClippingStartupAndFourTermExpiry()
    {
        var bars = new[] { 0d, 8, 11, 11, 11, 11, 11 }.Select(v => Candle(v)).ToArray(); var ratio = Math.Sqrt(72d / 73);
        var expected = new[] { 0d, 1, 1 + ratio, 1 + ratio, 1 + ratio, ratio, 0 }; var reference = BuiltInFormulaReferences.EhlersClipOutputs(bars, 1, 2, 1)["Esci"];
        for (var i = 0; i < expected.Length; i++) Assert.InRange(Math.Abs(expected[i] - reference[i]), 0d, 4e-16); Check(bars, 1, 2, 1);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, 1, int.MaxValue, int.MaxValue, kind); Check(bars, int.MaxValue, 1, 1, kind); Check(bars, 1, 2, 3, kind, int.MaxValue); }
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void NormalizationRetainsOverflowingAndSubnormalDerivatives()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 8 })
        {
            var bars = Enumerable.Range(0, 70).Select(i => Candle((i % 7 - 3) * scale)).ToArray();
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(bars, 2, 5, 3, kind);
        }
        var extreme = new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0d }.Select(v => Candle(v)).ToArray(); Check(extreme, 1, 2, 2);
        var tiny = new[] { 0d, double.Epsilon, 0d }.Select(v => Candle(v)).ToArray(); Assert.Equal(new[] { 0d, 1, 0 }, BuiltInFormulaReferences.EhlersClipOutputs(tiny, 1, 1, 1)["Esci"]); Check(tiny, 1, 1, 1);
        var ordinary = new[] { 0d, 8, 11, 11, 0, 2, 1 }.Select(v => Candle(v)).ToArray(); var scaled = ordinary.Select(b => Candle(b.Close * Math.ScaleB(1, 600))).ToArray();
        foreach (var key in new[] { "Esci", "Signal" }) Assert.Equal(BuiltInFormulaReferences.EhlersClipOutputs(ordinary, 1, 3, 2)[key], BuiltInFormulaReferences.EhlersClipOutputs(scaled, 1, 3, 2)[key]);
    }
    [Fact]
    public void SelectedInputDrivesDerivativeAndRms()
    {
        var bars = Enumerable.Range(0, 70).Select(i => Candle(2)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (i % 5 - 2) * 1e200).ToArray();
        var expected = BuiltInFormulaReferences.EhlersClipOutputs(bars.Select((b, i) => Candle(selected[i])).ToArray()); Assert.Contains(expected["Esci"], v => v != 0);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            var direct = Data(bars); direct.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeEhlersSimpleClipIndicatorFast(direct, context, outputKey: key); Assert.Equal(expected[key], raw.ToArray());
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersSimpleClipIndicator, new EhlersSimpleClipIndicatorSpecOptions(), key), context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray());
        }
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); batch.CalculateEhlersSimpleClipIndicator(); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
    }
    [Fact]
    public void CustomerSignalCallbackConsumesTheClippedLine()
    {
        var bars = new[] { 1d, 2, 3, 4 }.Select(v => Candle(v)).ToArray();
        foreach (var batch in new[] { false, true }) foreach (var key in new[] { "Esci", "Signal" })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 0d, 1, 2, 3 }, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var expected = key == "Signal" ? Enumerable.Repeat(42d, bars.Length).ToArray() : new[] { 0d, 1, 2, 3 };
            if (batch) Assert.Equal(expected, Data(bars).CalculateEhlersSimpleClipIndicator(length1: 1, length3: 1, signalLength: 3).OutputValues[key]);
            else { using var raw = IndicatorCompute.ComputeEhlersSimpleClipIndicatorFast(Data(bars), context, 1, 1, 3, outputKey: key); Assert.Equal(expected, raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersSimpleClipIndicatorState(); using var control = new EhlersSimpleClipIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
