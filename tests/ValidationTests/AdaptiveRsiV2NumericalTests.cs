using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveRsiV2NumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close) => new(DateTime.UnixEpoch, 0, 4, -4, close, 1);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    private static IStreamingIndicatorState State(bool fisher, int upper, int lower, int lag, MovingAvgType kind) => fisher ? new EhlersAdaptiveRsiFisherTransformV2State(kind, upper, lower, lag) : new EhlersAdaptiveRelativeStrengthIndexV2State(kind, upper, lower, lag);
    private static StockData Batch(StockData data, bool fisher, int upper, int lower, int lag, MovingAvgType kind) => fisher ? data.CalculateEhlersAdaptiveRsiFisherTransformV2(kind, upper, lower, lag) : data.CalculateEhlersAdaptiveRelativeStrengthIndexV2(kind, upper, lower, lag);
    private static ComputeBuffer Fast(StockData data, ComputeContext context, bool fisher, int upper, int lower, int lag, MovingAvgType kind, string? key = null) => fisher ? IndicatorCompute.ComputeEhlersAdaptiveRsiFisherTransformV2Fast(data, context, upper, lower, lag, kind) : IndicatorCompute.ComputeEhlersAdaptiveRelativeStrengthIndexV2Fast(data, context, upper, lower, lag, kind, key);
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar{i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveRelativeStrengthIndexV2) || c.IndicatorType == typeof(EhlersAdaptiveRsiFisherTransformV2)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptiveTravelRatios(IndicatorValidationCase c, string route)
    {
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var settings = options is EhlersAdaptiveRelativeStrengthIndexV2SpecOptions rsi ? (rsi.Length1, rsi.Length2, rsi.Length3, rsi.MaType, false) : options is EhlersAdaptiveRsiFisherTransformV2SpecOptions fish ? (fish.Length1, fish.Length2, fish.Length3, fish.MaType, true) : throw new InvalidOperationException();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AdaptiveRsiV2Values(bars, settings.Item1, settings.Item2, settings.Item3, Kind(settings.Item4), settings.Item5).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, bool fisher = false, int upper = 9, int lower = 3, int lag = 1, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.AdaptiveRsiV2Values(bars, upper, lower, lag, Kind(kind), fisher); var batch = Batch(Data(bars), fisher, upper, lower, lag, kind); var primary = fisher ? "Earsift" : "Earsi"; Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(expected.Outputs[primary], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], batch.OutputValues[key]); using var result = Fast(Data(bars), context, fisher, upper, lower, lag, kind, key); Equal(expected.Outputs[key], result.ToArray()); }
        var state = State(fisher, upper, lower, lag, kind);
        try
        {
            for (var replay = 0; replay < 2; replay++)
            {
                for (var i = 0; i < 22; i++) state.Update(Native(Candle(Math.Sin(i * .3))), true, false); state.Reset();
                for (var i = 0; i < bars.Length; i++) { state.Update(Native(Candle(-double.MaxValue)), false, false); foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.True(IndicatorErrorBudget.Exact.Accepts(expected.Outputs[primary][i], point.Value)); foreach (var key in expected.Outputs.Keys) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected.Outputs[key][i], point.Outputs![key]), $"bar{i}/{key}: {expected.Outputs[key][i]:R} != {point.Outputs[key]:R}"); } }
            }
        }
        finally { ((IDisposable)state).Dispose(); }
        return expected.Outputs;
    }
    private static Bar[] Wave(double scale = 1, int count = 65) => Enumerable.Range(0, count).Select(i => Candle((Math.Sin(i * .37) + .13 * Math.Cos(i * .79)) * .8 * scale)).ToArray();
    [Fact]
    public void WideAndSubnormalRoofingPreservesRsiAndFisher()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) foreach (var fisher in new[] { false, true })
        { var result = Check(Wave(scale), fisher); Assert.All(result.Values.SelectMany(v => v), v => Assert.True(double.IsFinite(v))); }
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(Wave(), kind: kind);
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsKeepLazyHistories()
    {
        foreach (var fisher in new[] { false, true })
        {
            Check(Array.Empty<Bar>(), fisher); var zeros = Check(Enumerable.Repeat(Candle(0), 55).ToArray(), fisher); if (!fisher) Assert.All(zeros["Earsi"], value => Assert.Equal(0, value));
            foreach (var periods in new[] { (int.MinValue, 0, -1), (1, 1, 1), (2, 1, 0), (9, 3, 10), (9, 3, int.MaxValue), (int.MaxValue, 3, 1), (int.MaxValue, int.MaxValue, 1), (2, int.MaxValue, 1) }) Check(Wave(count: 35), fisher, periods.Item1, periods.Item2, periods.Item3);
        }
    }
    [Fact]
    public void LongSmoothingRetainsNonzeroDcDrive()
    {
        var result = Check(Wave(), upper: 2, lower: int.MaxValue); Assert.Contains(result["Earsi"], value => value > 0); Assert.All(result["Earsi"], value => Assert.True(value < 1e-12));
    }
    [Fact]
    public void PowerOfTwoScalingPreservesAdaptiveOutputs()
    {
        foreach (var fisher in new[] { false, true }) { var expected = Check(Wave(), fisher); foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) { var actual = Check(Wave(scale), fisher); foreach (var key in expected.Keys) Equal(expected[key], actual[key]); } }
    }
    [Fact]
    public void SelectedPricesReachBatchAndEveryFastOutput()
    {
        var selected = Wave().Select(b => b.Close).ToList(); var original = selected.Select(_ => Candle(100)).ToArray();
        foreach (var fisher in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.AdaptiveRsiV2Values(Wave(), 9, 3, 1, fisher: fisher); var data = Data(original); data.SetCustomValues(selected); Batch(data, fisher, 9, 3, 1, MovingAvgType.ExponentialMovingAverage); Assert.Equal(expected.Signals, data.SignalsList);
            foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(original); source.SetCustomValues(selected); using var context = new ComputeContext(); using var actual = Fast(source, context, fisher, 9, 3, 1, MovingAvgType.ExponentialMovingAverage, key); Equal(expected.Outputs[key], actual.ToArray()); }
        }
    }
    [Fact]
    public void CustomerAverageIsConsumedOnceAndControlsOnlySignalLine()
    {
        var bars = Wave(); var raw = BuiltInFormulaReferences.AdaptiveRsiV2Values(bars, 9, 3, 1).Outputs["Earsi"]; var mean = Enumerable.Range(0, bars.Length).Select(i => i % 2 == 0 ? 1d : -1d).ToArray();
        foreach (var fisher in new[] { false, true }) foreach (var route in new[] { "batch", "main", "signal" })
        {
            if (fisher && route == "signal") continue; var expected = BuiltInFormulaReferences.AdaptiveRsiV2Values(bars, 9, 3, 1, fisher: fisher, externalAverage: mean);
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(3, period); Equal(raw, values); return mean; });
            if (route == "batch") { var actual = Batch(Data(bars), fisher, 9, 3, 1, MovingAvgType.ExponentialMovingAverage); Assert.Equal(expected.Signals, actual.SignalsList); foreach (var key in expected.Outputs.Keys) Equal(expected.Outputs[key], actual.OutputValues[key]); }
            else { var key = route == "signal" ? "Signal" : fisher ? "Earsift" : "Earsi"; using var context = new ComputeContext(); using var actual = Fast(Data(bars), context, fisher, 9, 3, 1, MovingAvgType.ExponentialMovingAverage, key); Equal(expected.Outputs[key], actual.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceCyclesRoofingOrAverages()
    {
        foreach (var fisher in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(fisher, 7, 3, 1, MovingAvgType.ExponentialMovingAverage); var control = State(fisher, 7, 3, 1, MovingAvgType.ExponentialMovingAverage);
            try
            {
                foreach (var bar in Wave(count: 15)) { state.Update(Native(bar), true, false); control.Update(Native(bar), true, false); }
                var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
                foreach (var bar in Wave(count: 12)) { var expected = control.Update(Native(bar), true, true); var actual = state.Update(Native(bar), true, true); Assert.Equal(expected.Value, actual.Value); foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]); }
            }
            finally { ((IDisposable)state).Dispose(); ((IDisposable)control).Dispose(); }
        }
    }
}
