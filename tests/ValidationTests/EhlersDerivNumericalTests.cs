using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EhlersDerivNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersSimpleDerivIndicator)).Select(c => new object[] { c });
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
    { var options = (EhlersSimpleDerivIndicatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.EhlersDerivOutputs(bars, options.Length, options.SignalLength, Kind(options.MaType)); }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 2, int signal = 8, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.EhlersDerivOutputs(bars, length, signal, Kind(kind)); var batch = Data(bars).CalculateEhlersSimpleDerivIndicator(kind, length, signal);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeEhlersSimpleDerivIndicatorFast(Data(bars), context, length, signal, kind, key); Assert.Equal(expected[key], raw.ToArray()); }
        using var state = new EhlersSimpleDerivIndicatorState(kind, length, signal);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Esdi"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void HandFourTermDerivativeSumAndLagStartup()
    {
        var bars = new[] { 1d, 2, 4, 8, 16 }.Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.EhlersDerivOutputs(bars, 1, 1); Assert.Equal(new[] { 0d, 1, 3, 7, 15 }, expected["Esdi"]); Assert.Equal(expected["Esdi"], expected["Signal"]); Check(bars, 1, 1);
        Assert.Equal(new[] { 0d, 0, 3, 9, 21 }, BuiltInFormulaReferences.EhlersDerivOutputs(bars)["Esdi"]); Check(bars);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, 1, 3, kind); Check(bars, int.MaxValue, int.MaxValue, kind); Check(bars, 1, int.MaxValue, kind); }
        Check(Array.Empty<Bar>());
    }
    [Fact]
    public void ExtendedLagDifferencesCancelAndRecover()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 100).Select(i => Candle(i < 80 ? (i % 5 - 2) / 2d * scale : 1)).ToArray(); Check(bars); Check(bars, 1, 3, MovingAvgType.WeightedMovingAverage);
        }
        var recovery = new[] { -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue }.Concat(Enumerable.Repeat(1d, 20)).Select(v => Candle(v)).ToArray();
        var expected = BuiltInFormulaReferences.EhlersDerivOutputs(recovery, 1, 1); Assert.True(double.IsPositiveInfinity(expected["Esdi"][1])); Assert.Equal(0, expected["Esdi"][2]); Assert.Equal(0, expected["Esdi"][^1]); Check(recovery, 1, 1); Check(recovery, 1, 2, MovingAvgType.WeightedMovingAverage);
    }
    [Fact]
    public void SelectedPricesDriveDirectRawAndDispatcher()
    {
        var selected = Enumerable.Range(0, 80).Select(i => i % 5 == 0 ? double.MaxValue : i % 5 == 1 ? -double.MaxValue : 1 + i % 7).ToArray(); var bars = selected.Select(_ => Candle(0)).ToArray(); var expected = BuiltInFormulaReferences.EhlersDerivOutputs(selected.Select(v => Candle(v)).ToArray());
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Contains(expected[key], value => value != 0); var direct = Data(bars); direct.SetCustomValues(selected.ToList());
            using var raw = IndicatorCompute.ComputeEhlersSimpleDerivIndicatorFast(direct, context, outputKey: key); Assert.Equal(expected[key], raw.ToArray());
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var arm = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersSimpleDerivIndicator, new EhlersSimpleDerivIndicatorSpecOptions(), key), context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray());
        }
        var batch = Data(bars); batch.SetCustomValues(selected.ToList()); var result = batch.CalculateEhlersSimpleDerivIndicator(); foreach (var key in expected.Keys) Assert.Equal(expected[key], result.OutputValues[key]);
    }
    [Fact]
    public void CustomerCallbackReceivesFourTermSumAndSignalPeriod()
    {
        var bars = new[] { 1d, 2, 4, 8, 16 }.Select(v => Candle(v)).ToArray();
        foreach (var batch in new[] { false, true }) foreach (var key in new[] { "Esdi", "Signal" })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 0d, 1, 3, 7, 15 }, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            using var context = new ComputeContext(); var output = key == "Esdi" ? new[] { 0d, 1, 3, 7, 15 } : Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Assert.Equal(output, Data(bars).CalculateEhlersSimpleDerivIndicator(length: 1, signalLength: 3).OutputValues[key]);
            else { using var raw = IndicatorCompute.ComputeEhlersSimpleDerivIndicatorFast(Data(bars), context, 1, 3, outputKey: key); Assert.Equal(output, raw.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceMediansOrRanges()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersSimpleDerivIndicatorState(); using var control = new EhlersSimpleDerivIndicatorState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
