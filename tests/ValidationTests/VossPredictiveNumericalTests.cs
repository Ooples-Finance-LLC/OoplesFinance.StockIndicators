using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class VossPredictiveNumericalTests
{
    private static readonly IndicatorErrorBudget Budget = IndicatorErrorBudget.Exact;
    private static void Equal(double[] expected, double[] actual) { Assert.Equal(expected.Length, actual.Length); for (var i = 0; i < expected.Length; i++) Assert.True(Budget.Accepts(expected[i], actual[i]), $"bar {i}: {expected[i]:R} != {actual[i]:R}"); }
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BEL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar Candle(double close, double high = 4, double low = 0) => new(DateTime.UnixEpoch, 0, high, low, close, 1);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EhlersVossPredictiveFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentWindow(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => Expected(bars, c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Dictionary<string, double[]> Expected(IReadOnlyList<Bar> bars, IIndicator indicator)
    { var options = (EhlersVossPredictiveFilterSpecOptions)((IBuiltInIndicator)indicator).CreateOptions(); return BuiltInFormulaReferences.VossPredictiveValues(bars, options.Length, options.Predict, options.Bandwidth); }
    private static void Check(Bar[] bars, int length, double predict, double bw)
    {
        var expected = BuiltInFormulaReferences.VossPredictiveValues(bars, length, predict, bw);
        var batch = Data(bars).CalculateEhlersVossPredictiveFilter(length, predict, bw);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var result = IndicatorCompute.ComputeEhlersVossPredictiveFilterFast(Data(bars), context, length, predict, bw, key); Assert.Equal(expected[key], result.ToArray());
            var core = new double[bars.Length]; OscillatorCore.EhlersVossPredictiveFilter(bars.Select(b => b.Close).ToArray(), core, length, predict, bw, key == "Filt"); Assert.Equal(expected[key], core);
        }
        using var state = new EhlersVossPredictiveFilterState(length, predict, bw);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Update(Native(Candle(-10)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Candle(double.MaxValue)), false, false);
                foreach (var commit in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), commit, true); Assert.Equal(expected["Voss"][i], point.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], point.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void WideFeedbackAndAllPredictionOrdersMatchRationalStages()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var length in new[] { 1, 2, 20, int.MaxValue })
        foreach (var predict in new[] { -double.MaxValue, 0, 1, 3, 177, double.MaxValue })
        {
            var bars = Enumerable.Range(0, 24).Select(i => Candle(i < 15 ? (i % 3 == 0 ? -scale : scale) : 0)).ToArray();
            Check(bars, length, predict, .25); Check(Array.Empty<Bar>(), length, predict, .25);
        }
        Check(Enumerable.Range(0, 24).Select(i => Candle(i % 3)).ToArray(), 20, 3, double.MaxValue);
    }
    [Fact]
    public void StartupAndSaturatedOrderHaveAnExplicitFirstImpulse()
    {
        var bars = Enumerable.Range(0, 12).Select(i => Candle(i == 6 ? 1 : 0)).ToArray();
        var cosine = Math.Cos(.25 * 2 * Math.PI / 20); var decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1); var drive = .5 * (1 - decay);
        foreach (var pair in new[] { (Predict: 0d, Order: 2), (Predict: 3d, Order: 9), (Predict: double.MaxValue, Order: 530) })
        {
            var expected = BuiltInFormulaReferences.VossPredictiveValues(bars, 20, pair.Predict, .25);
            Assert.All(expected["Voss"].Take(6), value => Assert.Equal(0, value)); Assert.All(expected["Filt"].Take(6), value => Assert.Equal(0, value));
            Assert.Equal(drive, expected["Filt"][6]); Assert.True(drive > 0); Assert.Equal((3d + pair.Order) / 2 * drive, expected["Voss"][6]);
            Check(bars, 20, pair.Predict, .25);
        }
    }
    [Fact]
    public void SelectedValuesDriveBothPublishedOutputs()
    {
        var selected = Enumerable.Range(0, 30).Select(i => (double)(i % 7)).ToList(); var original = selected.Select(_ => Candle(100)).ToArray();
        var expected = BuiltInFormulaReferences.VossPredictiveValues(selected.Select(v => Candle(v)).ToArray(), 20, 3, .25);
        var batch = Data(original); batch.SetCustomValues(selected); batch.CalculateEhlersVossPredictiveFilter();
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]); var data = Data(original); data.SetCustomValues(selected);
            using var result = IndicatorCompute.ComputeEhlersVossPredictiveFilterFast(data, context, outputKey: key); Assert.Equal(expected[key], result.ToArray());
        }
    }
    [Fact]
    public void NonfinitePredictionAndInvalidPhaseAreRejectedBeforeCalculation()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersVossPredictiveFilterState(predict: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersVossPredictiveFilter(predict: invalid));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeEhlersVossPredictiveFilterFast(Data(Array.Empty<Bar>()), context, predict: invalid));
        }
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EhlersVossPredictiveFilterState(length: 1, bw: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateEhlersVossPredictiveFilter(length: 1, bw: invalid));
            using var context = new ComputeContext(); Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeEhlersVossPredictiveFilterFast(Data(Array.Empty<Bar>()), context, length: 1, bw: invalid));
        }
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceWindowOrPreviousFilteredValue()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new EhlersVossPredictiveFilterState(); using var control = new EhlersVossPredictiveFilterState();
            var seed = Native(Candle(2)); state.Update(seed, true, false); control.Update(seed, true, false);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var bar in Enumerable.Range(0, 12).Select(i => Native(Candle(i % 3))))
                Assert.Equal(control.Update(bar, true, true).Value, state.Update(bar, true, true).Value);
        }
    }
}
