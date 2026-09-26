using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DynamicAverageNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactWindowsPreserveExtremeValuesAndPreviewReset()
    {
        foreach (var (fast, slow) in new[] { (1, 1), (2, 4), (3, 7), (7, 3), (6, 200), (2, int.MaxValue) })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 3, 2, 8, -3, 0, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0, 0, 0 }, Enumerable.Repeat(double.MaxValue, 30).ToArray() })
        {
            var bars = BarsOf(prices); var indicator = new DynamicallyAdjustableMovingAverage(fast, slow);
            var expected = BuiltInFormulaReferences.DynamicAverageOutputs(bars, indicator)["Dama"];
            Assert.Equal(expected, Data(bars).CalculateDynamicallyAdjustableMovingAverage(fast, slow).CustomValuesList);
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.MovingAverageCore.DynamicallyAdjustableMovingAverage(prices, core, fast, slow); Assert.Equal(expected, core);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeDynamicallyAdjustableMovingAverageFast(Data(bars), context, fast, slow); Assert.Equal(expected, raw.Span.ToArray());
            using var state = new DynamicallyAdjustableMovingAverageState(fast, slow);
            for (var replay = 0; replay < 2; replay++)
            {
                foreach (var seed in BarsOf(new[] { 99d, -31, 7 })) state.Update(Native(seed), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }
    [Fact]
    public void HandComputedPopulationRatioAndZeroPadding()
    {
        var bars = BarsOf(new[] { 1d, 3, 5, 7 });
        // First three periods are 2; at bar four sqrt(5)/1+2 rounds to 4.
        Assert.Equal(new[] { .5, 2, 4, 4 }, BuiltInFormulaReferences.DynamicAverageOutputs(bars, new DynamicallyAdjustableMovingAverage(2, 4))["Dama"]);
        Assert.Equal(new[] { .5, 2, 4, 4 }, Data(bars).CalculateDynamicallyAdjustableMovingAverage(2, 4).CustomValuesList);
    }
    [Fact]
    public void HalfwayPeriodsRoundToEvenWithoutRoundingTheVariance()
    {
        foreach (var (fast, slow, suffix, expected) in new[]
        {
            (2, 32, new[] { 0d, 2, 0, 2, 0, 2, 0, 2 }, 1d),
            (3, 48, new[] { 0d, 2, 0, 2, 0, 2, 2, 0, 1 }, 1.25)
        })
        {
            var prices = Enumerable.Repeat(1d, slow - suffix.Length).Concat(suffix).ToArray(); var bars = BarsOf(prices);
            Assert.Equal(expected, BuiltInFormulaReferences.DynamicAverageOutputs(bars, new DynamicallyAdjustableMovingAverage(fast, slow))["Dama"][^1]);
            var window = new DynamicAverageWindow(fast, slow); double value = 0;
            foreach (var price in prices) value = window.Next(price, true);
            Assert.Equal(expected, value);
        }
    }
    [Fact]
    public void RawAndBatchRespectSelectedInput()
    {
        var selected = new[] { 1d, 3, -2, 4 }; var data = Data(BarsOf(new[] { 20d, 40, 30, 80 })); data.SetCustomValues(selected.ToList());
        var expected = BuiltInFormulaReferences.DynamicAverageOutputs(BarsOf(selected), new DynamicallyAdjustableMovingAverage(2, 4))["Dama"];
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeDynamicallyAdjustableMovingAverageFast(data, context, 2, 4);
        Assert.Equal(expected, raw.Span.ToArray()); Assert.Equal(expected, data.CalculateDynamicallyAdjustableMovingAverage(2, 4).CustomValuesList);
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new DynamicallyAdjustableMovingAverageState(2, 4);
            IStreamingIndicatorState control = new DynamicallyAdjustableMovingAverageState(2, 4);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }


    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DynamicallyAdjustableMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.DynamicAverageOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}
