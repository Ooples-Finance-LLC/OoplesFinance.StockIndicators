using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AdaptiveRangeV2NumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close) => new(DateTime.UnixEpoch, 0, 4, -4, close, 1);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.WildersSmoothingMethod ? 6 : 3;
    private static string Primary(int mode) => mode == 2 ? "Eacci" : mode == 1 ? "Easift" : "Easi";
    private static IStreamingIndicatorState State(int mode, int upper, int lower, int lag, MovingAvgType kind) => mode == 2 ? new EhlersAdaptiveCommodityChannelIndexV2State(kind, upper, lower, lag) : mode == 1 ? new EhlersAdaptiveStochasticInverseFisherTransformState(kind, upper, lower, lag) : new EhlersAdaptiveStochasticIndicatorV2State(kind, upper, lower, lag);
    private static StockData Batch(StockData data, int mode, int upper, int lower, int lag, MovingAvgType kind) => mode == 2 ? data.CalculateEhlersAdaptiveCommodityChannelIndexV2(kind, upper, lower, lag) : mode == 1 ? data.CalculateEhlersAdaptiveStochasticInverseFisherTransform(kind, upper, lower, lag) : data.CalculateEhlersAdaptiveStochasticIndicatorV2(kind, upper, lower, lag);
    private static ComputeBuffer Fast(StockData data, ComputeContext context, int mode, int upper, int lower, int lag, MovingAvgType kind, string? key = null) => mode == 2 ? IndicatorCompute.ComputeEhlersAdaptiveCommodityChannelIndexV2Fast(data, context, upper, lower, lag, kind, key) : mode == 1 ? IndicatorCompute.TryComputeFast(data, new IndicatorSpec(IndicatorName.EhlersAdaptiveStochasticInverseFisherTransform, new EhlersAdaptiveStochasticInverseFisherTransformSpecOptions(upper, lower, lag, kind), key), context) ?? throw new InvalidOperationException("Missing inverse fast route") : IndicatorCompute.ComputeEhlersAdaptiveStochasticIndicatorV2Fast(data, context, upper, lower, lag, kind, key);
    private static void Equal(double[] expected, IEnumerable<double> actual) { var values = actual.ToArray(); Assert.Equal(expected.Length, values.Length); for (var i = 0; i < values.Length; i++) Assert.True(IndicatorErrorBudget.Exact.Accepts(expected[i], values[i]), $"bar{i}: {expected[i]:R} != {values[i]:R}"); }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersAdaptiveStochasticIndicatorV2) || c.IndicatorType == typeof(EhlersAdaptiveStochasticInverseFisherTransform) || c.IndicatorType == typeof(EhlersAdaptiveCommodityChannelIndexV2)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentAdaptiveRanges(IndicatorValidationCase c, string route)
    {
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions(); var settings = options is EhlersAdaptiveStochasticIndicatorV2SpecOptions stoch ? (stoch.Length1, stoch.Length2, stoch.Length3, stoch.MaType, 0) : options is EhlersAdaptiveStochasticInverseFisherTransformSpecOptions inverse ? (inverse.Length1, inverse.Length2, inverse.Length3, inverse.MaType, 1) : options is EhlersAdaptiveCommodityChannelIndexV2SpecOptions cci ? (cci.Length1, cci.Length2, cci.Length3, cci.MaType, 2) : throw new InvalidOperationException();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.AdaptiveRangeV2Values(bars, settings.Item1, settings.Item2, settings.Item3, Kind(settings.Item4), settings.Item5).Outputs, IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Check(Bar[] bars, int mode = 0, int upper = 9, int lower = 3, int lag = 1, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.AdaptiveRangeV2Values(bars, upper, lower, lag, Kind(kind), mode); var batch = Batch(Data(bars), mode, upper, lower, lag, kind); var primary = Primary(mode); Assert.Equal(expected.Outputs.Keys, batch.OutputValues.Keys); Assert.Equal(expected.Signals, batch.SignalsList); Equal(expected.Outputs[primary], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], batch.OutputValues[key]); using var result = Fast(Data(bars), context, mode, upper, lower, lag, kind, key); Equal(expected.Outputs[key], result.ToArray()); }
        var state = State(mode, upper, lower, lag, kind);
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
    public void WideAndSubnormalRoofingPreservesRangesAndInverseFisher()
    {
        foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -1000), 1d, Math.Pow(2, 1000), double.MaxValue }) foreach (var mode in new[] { 0, 1, 2 })
        { var result = Check(Wave(scale), mode); Assert.All(result.Values.SelectMany(v => v), v => Assert.True(double.IsFinite(v))); }
        foreach (var mode in new[] { 0, 2 }) foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.WildersSmoothingMethod }) Check(Wave(), mode, kind: kind);
    }
    [Fact]
    public void EmptyZeroAndExtremePeriodsKeepLazyHistories()
    {
        foreach (var mode in new[] { 0, 1, 2 })
        {
            Check(Array.Empty<Bar>(), mode); var zeros = Check(Enumerable.Repeat(Candle(0), 55).ToArray(), mode); if (mode != 1) Assert.All(zeros[Primary(mode)], value => Assert.Equal(0, value));
            foreach (var periods in new[] { (int.MinValue, 0, -1), (1, 1, 1), (2, 1, 0), (9, 3, 10), (9, 3, int.MaxValue), (int.MaxValue, 3, 1), (int.MaxValue, int.MaxValue, 1), (2, int.MaxValue, 1) }) Check(Wave(count: 35), mode, periods.Item1, periods.Item2, periods.Item3);
        }
    }
    [Fact]
    public void LongSmoothingRetainsNonzeroDcDrive()
    {
        foreach (var mode in new[] { 0, 2 }) { var result = Check(Wave(), mode, upper: 2, lower: int.MaxValue); Assert.Contains(result[Primary(mode)], value => value > 0); Assert.All(result[Primary(mode)], value => Assert.True(Math.Abs(value) < 1e-9)); }
    }
    [Fact]
    public void PowerOfTwoScalingPreservesAdaptiveOutputs()
    {
        foreach (var mode in new[] { 0, 1, 2 }) { var expected = Check(Wave(), mode); foreach (var scale in new[] { Math.Pow(2, -500), Math.Pow(2, 500) }) { var actual = Check(Wave(scale), mode); foreach (var key in expected.Keys) Equal(expected[key], actual[key]); } }
    }
    [Fact]
    public void CciSquaredResidualsSurviveBothExponentExtremes()
    {
        var expected = Check(Wave(), 2); Assert.Contains(expected["Eacci"], value => Math.Abs(value) > 10);
        foreach (var scale in new[] { Math.Pow(2, -800), Math.Pow(2, 800) }) { var actual = Check(Wave(scale), 2); foreach (var key in expected.Keys) Equal(expected[key], actual[key]); }
    }
    [Fact]
    public void SelectedPricesReachBatchAndEveryFastOutput()
    {
        var selected = Wave().Select(b => b.Close).ToList(); var original = selected.Select(_ => Candle(100)).ToArray();
        foreach (var mode in new[] { 0, 1, 2 })
        {
            var expected = BuiltInFormulaReferences.AdaptiveRangeV2Values(Wave(), 9, 3, 1, mode: mode); var data = Data(original); data.SetCustomValues(selected); Batch(data, mode, 9, 3, 1, MovingAvgType.ExponentialMovingAverage); Assert.Equal(expected.Signals, data.SignalsList);
            foreach (var key in expected.Outputs.Keys) { Equal(expected.Outputs[key], data.OutputValues[key]); var source = Data(original); source.SetCustomValues(selected); using var context = new ComputeContext(); using var actual = Fast(source, context, mode, 9, 3, 1, MovingAvgType.ExponentialMovingAverage, key); Equal(expected.Outputs[key], actual.ToArray()); }
        }
    }
    [Fact]
    public void CustomerAverageIsConsumedOnceAndPreservesInverseTrigger()
    {
        var bars = Wave(); var mean = Enumerable.Range(0, bars.Length).Select(i => i % 2 == 0 ? 1000d : -1000d).ToArray();
        foreach (var mode in new[] { 0, 1, 2 }) foreach (var route in new[] { "batch", "main", "signal" })
        {
            var rawMode = mode == 2 ? 2 : 0; var raw = BuiltInFormulaReferences.AdaptiveRangeV2Values(bars, 9, 3, 1, mode: rawMode).Outputs[Primary(rawMode)]; var expected = BuiltInFormulaReferences.AdaptiveRangeV2Values(bars, 9, 3, 1, mode: mode, externalAverage: mean);
            using var armed = ComponentAverage.Arm((values, period) => { Assert.Equal(3, period); Equal(raw, values); return mean; });
            if (route == "batch") { var actual = Batch(Data(bars), mode, 9, 3, 1, MovingAvgType.ExponentialMovingAverage); Assert.Equal(expected.Signals, actual.SignalsList); foreach (var key in expected.Outputs.Keys) Equal(expected.Outputs[key], actual.OutputValues[key]); }
            else { var key = route == "signal" ? "Signal" : Primary(mode); using var context = new ComputeContext(); using var actual = Fast(Data(bars), context, mode, 9, 3, 1, MovingAvgType.ExponentialMovingAverage, key); Equal(expected.Outputs[key], actual.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceCyclesRoofingOrAverages()
    {
        foreach (var mode in new[] { 0, 1, 2 }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(mode, 7, 3, 1, MovingAvgType.ExponentialMovingAverage); var control = State(mode, 7, 3, 1, MovingAvgType.ExponentialMovingAverage);
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
