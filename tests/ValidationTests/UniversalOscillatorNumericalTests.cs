using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UniversalOscillatorNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersUniversalOscillator)).Select(c => new object[] { c });
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
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static readonly MovingAvgType[] Averages = { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod };
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var o = (EhlersUniversalOscillatorSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.UniversalOscillatorOutputs(bars, o.Length, Kind(o.MaType)); }
    private static void Check(Bar[] bars, int length = 20, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage, int signalLength = 9)
    {
        var expected = BuiltInFormulaReferences.UniversalOscillatorOutputs(bars, length, Kind(kind), signalLength);
        var batch = Data(bars).CalculateEhlersUniversalOscillator(kind, length, signalLength); foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        Assert.Equal(expected["Euo"], batch.CustomValuesList); Assert.All(expected["Euo"], v => Assert.InRange(v, -1d, 1d));
        using var context = new ComputeContext();
        if (signalLength == 9) foreach (var key in expected.Keys)
        { using var arm = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(IndicatorName.EhlersUniversalOscillator, new EhlersUniversalOscillatorSpecOptions(length, kind), key), context); Assert.NotNull(arm); Assert.Equal(expected[key], arm.Value.ToArray()); }
        var core = new double[bars.Length]; OscillatorCore.EhlersUniversalOscillator(bars.Select(b => b.Close).ToArray(), core, length); Assert.Equal(expected["Euo"], core);
        using var state = new EhlersUniversalOscillatorState(kind, length, signalLength);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(Candle(10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Euo"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void SignedExponentScalingPreservesTinyAndWideNormalization()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue }) foreach (var kind in Averages)
            Check(Enumerable.Range(0, 70).Select(i => Candle(i < 40 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray(), 20, kind);
    }
    [Fact]
    public void PowerOfTwoScalingPreservesTheEntireNormalizedTrajectory()
    {
        var unit = Enumerable.Range(0, 90).Select(i => Candle(i < 40 ? i % 5 - 2 : 0)).ToArray();
        var expected = Data(unit).CalculateEhlersUniversalOscillator().OutputValues;
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), Math.Pow(2, 1000) })
        {
            var bars = unit.Select(b => Candle(b.Close * scale)).ToArray(); var actual = Data(bars).CalculateEhlersUniversalOscillator().OutputValues;
            foreach (var key in expected.Keys) Assert.Equal(expected[key], actual[key]); Check(bars);
        }
    }
    [Fact]
    public void TwoBarStartupAndFirstTinyImpulseReachUnitPeak()
    {
        foreach (var length in new[] { 1, 20, int.MaxValue }) foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = new[] { Candle(0), Candle(0), Candle(scale), Candle(0), Candle(0) }; var line = Data(bars).CalculateEhlersUniversalOscillator(length: length).CustomValuesList;
            Assert.Equal(0d, line[0]); Assert.Equal(0d, line[1]); Assert.Equal(1d, line[2]); Check(bars, length);
        }
        Check(Enumerable.Repeat(Candle(0), 20).ToArray());
    }
    [Fact]
    public void PeriodsNormalizeIndependentlyAndSignalHistoryGrows()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 3)).ToArray();
        foreach (var kind in Averages) foreach (var pair in new[] { (int.MinValue, 0), (0, int.MaxValue), (int.MaxValue, -1), (int.MaxValue, int.MaxValue) })
        { Check(bars, pair.Item1, kind, pair.Item2); Check(Array.Empty<Bar>(), pair.Item1, kind, pair.Item2); }
    }
    [Fact]
    public void SelectedPricesReachBothOutputsAndTypedArms()
    {
        var selected = Enumerable.Range(0, 25).Select(i => (double)(i % 7)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var kind in Averages)
        {
            var expected = BuiltInFormulaReferences.UniversalOscillatorOutputs(selected.Select(v => Candle(v)).ToArray(), 20, Kind(kind)); var batch = Data(bars); batch.SetCustomValues(selected); batch.CalculateEhlersUniversalOscillator(kind);
            using var context = new ComputeContext();
            foreach (var key in expected.Keys)
            { Assert.Equal(expected[key], batch.OutputValues[key]); var data = Data(bars); data.SetCustomValues(selected); using var output = IndicatorCompute.ComputeArm(data, new IndicatorSpec(IndicatorName.EhlersUniversalOscillator, new EhlersUniversalOscillatorSpecOptions(20, kind), key), context); Assert.NotNull(output); Assert.Equal(expected[key], output.Value.ToArray()); }
        }
    }
    [Fact]
    public void CallbackReceivesNormalizedLineOnce()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i % 3)).ToArray(); var raw = BuiltInFormulaReferences.UniversalOscillatorOutputs(bars, 20, 3)["Euo"];
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (input, period) => { Assert.Equal(9, period); Assert.Equal(raw, input); return Enumerable.Repeat(42d, input.Count).ToArray(); } });
            var expected = Enumerable.Repeat(42d, bars.Length).ToArray();
            if (batch) Assert.Equal(expected, Data(bars).CalculateEhlersUniversalOscillator().OutputValues["Signal"]);
            else { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeEhlersUniversalSignalFast(Data(bars), context, 20, MovingAvgType.ExponentialMovingAverage); Assert.Equal(expected, output.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsLeaveFilterPeakAndSignalUnchanged()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersUniversalOscillatorState(); using var control = new EhlersUniversalOscillatorState(); var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 10).Select(i => Native(Candle(i % 3))))
            { var a = control.Update(bar, true, true); var b = state.Update(bar, true, true); Assert.Equal(a.Value, b.Value); foreach (var key in a.Outputs!.Keys) Assert.Equal(a.Outputs[key], b.Outputs![key]); }
        }
    }
}
