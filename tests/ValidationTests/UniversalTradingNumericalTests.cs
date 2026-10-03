using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UniversalTradingNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersUniversalTradingFilter) || c.IndicatorType == typeof(EhlersSnakeUniversalTradingFilter)).Select(c => new object[] { c });
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
        var o = ((IBuiltInIndicator)indicator).CreateOptions();
        if (o is EhlersSnakeUniversalTradingFilterSpecOptions snake) return BuiltInFormulaReferences.UniversalTradingValues(bars, snake.Length1, snake.Length2, snake.Bw, snake.MaType, true);
        var ordinary = (EhlersUniversalTradingFilterSpecOptions)o; return BuiltInFormulaReferences.UniversalTradingValues(bars, ordinary.Length1, ordinary.Length2, ordinary.Mult, ordinary.MaType, false);
    }
    private static readonly MovingAvgType[] Kinds = { MovingAvgType.EhlersHannMovingAverage, MovingAvgType.WeightedMovingAverage };
    private static StockData Batch(StockData data, bool snake, int length, int rms, double parameter, MovingAvgType kind) => snake ? data.CalculateEhlersSnakeUniversalTradingFilter(kind, length, rms, parameter) : data.CalculateEhlersUniversalTradingFilter(kind, length, rms, parameter);
    private static ComputeBuffer Fast(StockData data, ComputeContext context, bool snake, int length, int rms, double parameter, MovingAvgType kind, string key)
        => snake ? IndicatorCompute.ComputeEhlersSnakeUniversalTradingFilterFast(data, context, length, rms, parameter, kind, key == "UpperBand" ? IndicatorCompute.SnakeFilterSeries.UpperBand : key == "LowerBand" ? IndicatorCompute.SnakeFilterSeries.LowerBand : IndicatorCompute.SnakeFilterSeries.Erf) : IndicatorCompute.ComputeEhlersUniversalTradingFilterFast(data, context, length, rms, parameter, kind, key);
    private static IStreamingIndicatorState State(bool snake, int length, int rms, double parameter, MovingAvgType kind) => snake ? new EhlersSnakeUniversalTradingFilterState(kind, length, rms, parameter) : new EhlersUniversalTradingFilterState(kind, length, rms, parameter);
    private static Dictionary<string, double[]> Check(Bar[] bars, bool snake, int length = 5, int rms = 7, double parameter = 2, MovingAvgType kind = MovingAvgType.EhlersHannMovingAverage)
    {
        var expected = BuiltInFormulaReferences.UniversalTradingValues(bars, length, rms, parameter, kind, snake); var batch = Batch(Data(bars), snake, length, rms, parameter, kind); var primary = snake ? "Erf" : "Eutf"; Assert.Equal(expected[primary], batch.CustomValuesList);
        using var context = new ComputeContext(); foreach (var key in expected.Keys) { using var result = Fast(Data(bars), context, snake, length, rms, parameter, kind, key); Assert.Equal(expected[key], result.ToArray()); Assert.Equal(expected[key], batch.OutputValues[key]); }
        var state = State(snake, length, rms, parameter, kind); using var lifetime = (IDisposable)state;
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var seed in new[] { 1d, -5, 4, -2, 8, 1 }) state.Update(Native(Candle(seed)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(-double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true }) { var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected[primary][i], point.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]); }
            }
        }
        return expected;
    }
    [Fact]
    public void WideAndSubnormalValuesRetainFilterAndRms()
    {
        foreach (var snake in new[] { false, true }) foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, 32 * double.Epsilon, 1d, Math.Pow(2, 1000), double.MaxValue })
        {
            var result = Check(Enumerable.Range(0, 65).Select(i => Candle(i % 5 < 2 ? -scale : scale)).ToArray(), snake, kind: kind);
            Assert.All(result["UpperBand"], v => Assert.True(v >= 0)); for (var i = 0; i < result["UpperBand"].Length; i++) Assert.Equal(-result["UpperBand"][i], result["LowerBand"][i]);
        }
    }
    [Fact]
    public void RmsUsesAvailableSamplesAndExpiresOldEnergy()
    {
        var values = Check(new[] { 3d, 4, 0, 0, 0 }.Select(v => Candle(v)).ToArray(), false, 1, 2, 100);
        Assert.Equal(new[] { 3d, Math.Sqrt(12.5), Math.Sqrt(8), 0, 0 }, values["UpperBand"]);
        var tiny = Check(new[] { double.Epsilon, 0, 0 }.Select(v => Candle(v)).ToArray(), false, 1, 2, 100); Assert.Equal(double.Epsilon, tiny["UpperBand"][0]);
    }
    [Fact]
    public void ExtremePeriodsAndMomentumLagsUseConsumedHistory()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var snake in new[] { false, true }) foreach (var kind in Kinds) foreach (var length in new[] { int.MinValue, 0, 1, 2, int.MaxValue })
        { Check(bars, snake, length, length, kind: kind); Check(Array.Empty<Bar>(), snake, length, length, kind: kind); }
        foreach (var kind in Kinds) { var values = Check(bars, false, int.MaxValue, int.MaxValue, double.MaxValue, kind); Assert.Contains(values["Eutf"], v => v != 0); }
    }
    [Fact]
    public void ZeroAndFractionalMomentumLagsHaveDefinedStartup()
    {
        var bars = new[] { 1d, 2, 4, 8, 16 }.Select(v => Candle(v)).ToArray();
        foreach (var kind in Kinds) foreach (var series in Check(bars, false, 3, 2, 0, kind).Values) Assert.All(series, v => Assert.Equal(0, v));
        Assert.Equal(new[] { 1d, 2, 4, 7, 14 }, Check(bars, false, 1, 2, 2.1)["Eutf"]);
    }
    [Fact]
    public void HannMassAndLongPeriodGainRemainDefined()
    {
        var impulse = new[] { 1d, 0, 0, 0, 0, 0 }.Select(v => Candle(v)).ToArray(); var values = Check(impulse, false, 4, 3, 100)["Eutf"];
        Assert.InRange(Math.Abs(values.Sum() - 1), 0, 1e-15);
        var longPeriod = Check(new[] { 1d, 0, 0 }.Select(v => Candle(v)).ToArray(), false, int.MaxValue, 3, 100)["Eutf"]; Assert.All(longPeriod, v => Assert.True(v > 0));
    }
    [Fact]
    public void SnakeStartsAfterThreeSamplesAndClampsBandwidth()
    {
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var kind in Kinds) foreach (var width in new[] { -double.MaxValue, -1d, 0, 1.4, double.MaxValue }) Assert.All(Check(bars, true, parameter: width, kind: kind)["Erf"].Take(3), v => Assert.Equal(0, v));
    }
    [Fact]
    public void SelectedPricesReachEveryBatchAndFastOutput()
    {
        var selected = Enumerable.Range(0, 45).Select(i => (double)(i % 5 - 2)).ToList(); var bars = selected.Select(_ => Candle(100)).ToArray();
        foreach (var snake in new[] { false, true }) foreach (var kind in Kinds)
        {
            var expected = BuiltInFormulaReferences.UniversalTradingValues(selected.Select(v => Candle(v)).ToArray(), 3, 2, 1.4, kind, snake); var data = Data(bars); data.SetCustomValues(selected); Batch(data, snake, 3, 2, 1.4, kind);
            foreach (var key in expected.Keys) { Assert.Equal(expected[key], data.OutputValues[key]); var source = Data(bars); source.SetCustomValues(selected); using var context = new ComputeContext(); using var result = Fast(source, context, snake, 3, 2, 1.4, kind, key); Assert.Equal(expected[key], result.ToArray()); }
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceFilterOrEnergy()
    {
        foreach (var snake in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var commit in new[] { false, true })
        {
            var state = State(snake, 3, 2, 1.4, Kinds[0]); var control = State(snake, 3, 2, 1.4, Kinds[0]); using var lifetime = (IDisposable)state; using var other = (IDisposable)control;
            for (var i = 0; i < 10; i++) { state.Update(Native(Candle(i % 4)), true, false); control.Update(Native(Candle(i % 4)), true, false); }
            var values = new[] { 1d, 3, 0, 2, 1 }; values[field] = invalid; Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4])), commit, true));
            for (var i = 0; i < 15; i++) { var bar = Native(Candle(i % 5 - 2)); var expected = control.Update(bar, true, true); var actual = state.Update(bar, true, true); Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!["UpperBand"], actual.Outputs!["UpperBand"]); }
        }
    }
    [Fact]
    public void ComponentAverageFeedsRmsFromItsOwnOutput()
    {
        var bars = new[] { 1d, 3, 5, -2 }.Select(v => Candle(v)).ToArray(); var upper = new[] { 3d, Math.Sqrt(12.5), Math.Sqrt(8), 0 };
        foreach (var snake in new[] { false, true }) foreach (var fast in new[] { false, true }) foreach (var key in new[] { snake ? "Erf" : "Eutf", "UpperBand", "LowerBand" })
        {
            var calls = 0; using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] { (input, period) => { calls++; Assert.Equal(3, period); Assert.Equal(4, input.Count); Assert.All(input, v => Assert.True(double.IsFinite(v))); return new[] { 3d, 4, 0, 0 }; } }); double[] actual;
            if (fast) { using var context = new ComputeContext(); using var result = Fast(Data(bars), context, snake, 3, 2, 1.4, Kinds[0], key); actual = result.ToArray(); }
            else actual = Batch(Data(bars), snake, 3, 2, 1.4, Kinds[0]).OutputValues[key].ToArray();
            Assert.Equal(1, calls); Assert.Equal(key == "UpperBand" ? upper : key == "LowerBand" ? upper.Select(v => -v).ToArray() : new[] { 3d, 4, 0, 0 }, actual);
        }
    }
    [Fact]
    public void InvalidParametersAreRejectedAndFallbackSmoothingIsPreserved()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var snake in new[] { false, true }) Assert.Throws<ArgumentOutOfRangeException>(() => State(snake, 3, 2, invalid, Kinds[0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => State(false, 3, 2, -1, Kinds[0]));
        var bars = Enumerable.Range(0, 35).Select(i => Candle(i % 5 - 2)).ToArray();
        foreach (var snake in new[] { false, true })
        {
            var kind = MovingAvgType.SimpleMovingAverage; var batch = Batch(Data(bars), snake, 3, 2, 1.4, kind); var state = State(snake, 3, 2, 1.4, kind); using var lifetime = (IDisposable)state; var key = snake ? "Erf" : "Eutf";
            using var context = new ComputeContext(); using var result = Fast(Data(bars), context, snake, 3, 2, 1.4, kind, key); Assert.Equal(batch.OutputValues[key], result.ToArray()); for (var i = 0; i < bars.Length; i++) Assert.Equal(batch.OutputValues[key][i], state.Update(Native(bars[i]), true, false).Value);
        }
    }
}
