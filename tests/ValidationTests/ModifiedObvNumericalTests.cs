using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ModifiedObvNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(OnBalanceVolumeModified)).Select(c => new object[] { c });
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
    { var options = (OnBalanceVolumeModifiedSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.ModifiedObvOutputs(bars, options.Length1, options.Length2, Kind(options.MaType)); }
    private static int Kind(MovingAvgType kind) => kind switch { MovingAvgType.SimpleMovingAverage => 1, MovingAvgType.WeightedMovingAverage => 2, MovingAvgType.ExponentialMovingAverage => 3, _ => 6 };
    private static void Check(Bar[] bars, int length = 7, int signalLength = 10, MovingAvgType kind = MovingAvgType.ExponentialMovingAverage)
    {
        var expected = BuiltInFormulaReferences.ModifiedObvOutputs(bars, length, signalLength, Kind(kind)); var batch = Data(bars).CalculateOnBalanceVolumeModified(kind, length, signalLength);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys) { using var raw = IndicatorCompute.ComputeOnBalanceVolumeModifiedFast(Data(bars), context, length, signalLength, kind, key); Assert.Equal(expected[key], raw.ToArray()); }
        using var state = new OnBalanceVolumeModifiedState(kind, length, signalLength);
        for (var replay = 0; replay < 2; replay++)
        {
            foreach (var b in Enumerable.Range(0, 10).Select(i => Candle(i % 2 == 0 ? double.MaxValue : -double.MaxValue))) state.Update(Native(b), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var final in new[] { false, false, true })
                { var actual = state.Update(Native(bars[i]), final, true); Assert.Equal(expected["Obvm"][i], actual.Value); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void HandDirectionsTiesAndIndependentSignalPeriod()
    {
        var prices = new[] { 1d, 2, 2, -1, 0 }; var volumes = new[] { 2d, 4, 100, 3, 1 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch, 0, 4, -2, v, volumes[i])).ToArray();
        var expected = BuiltInFormulaReferences.ModifiedObvOutputs(bars, 1, 2, 1);
        Assert.Equal(new[] { 2d, 6, 6, 3, 4 }, expected["Obvm"]); Assert.Equal(new[] { 0d, 4, 6, 4.5, 3.5 }, expected["Signal"]);
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        { Check(bars, 1, 2, kind); Check(bars, 3, 2, kind); Check(bars, int.MaxValue, int.MaxValue, kind); Check(Array.Empty<Bar>(), 2, 3, kind); }
    }
    [Fact]
    public void ExactTotalAndUnpublishedStagesRecoverAfterOverflow()
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 48).Select(i => new Bar(DateTime.UnixEpoch, 0, 4, -2, i % 8 < 4 ? i % 8 : 8 - i % 8, scale)).ToArray();
            Check(bars, 3, 5, kind);
        }
        var cancellation = new[] { 1e100, 1d, -1e100, 2 }.Select((v, i) => new Bar(DateTime.UnixEpoch, 0, 4, -2, i + 1, v)).ToArray();
        Assert.Equal(new[] { 1e100, 1e100, 1, 3 }, BuiltInFormulaReferences.ModifiedObvOutputs(cancellation, 1, 1)["Obvm"]); Check(cancellation, 1, 1);
    }
    [Fact]
    public void SelectedPricesRetainOriginalVolume()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(DateTime.UnixEpoch, 0, 4, -2, 2, i + 1)).ToArray(); var selected = Enumerable.Range(0, bars.Length).Select(i => (double)(i % 5 - 2)).ToArray();
        var expected = BuiltInFormulaReferences.ModifiedObvOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray());
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList()); using var raw = IndicatorCompute.ComputeOnBalanceVolumeModifiedFast(data, context, outputKey: key); Assert.Equal(expected[key], raw.ToArray());
            var batch = Data(bars); batch.SetCustomValues(selected.ToList()); Assert.Equal(expected[key], batch.CalculateOnBalanceVolumeModified().OutputValues[key]);
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvancePriceOrPhaseHistory()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            var state = new OnBalanceVolumeModifiedState(); var control = new OnBalanceVolumeModifiedState(); var seed = Native(Candle(1)); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Enumerable.Range(0, 12).Select(i => Candle(i % 3))) Assert.Equal(control.Update(Native(b), true, true).Value, state.Update(Native(b), true, true).Value);
        }
    }
}
