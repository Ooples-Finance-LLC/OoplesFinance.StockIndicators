using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class HoltNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void CascadesPreserveStagesCancellationPreviewAndReset()
    {
        foreach (var length in new[] { 1, 2, 3, 7, int.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 },
            Enumerable.Repeat(double.MaxValue, 10).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new HoltExponentialMovingAverage(length);
            var expected = BuiltInFormulaReferences.HoltOutputs(bars, length).Values.Single();
            var batch = Data(bars).CalculateHoltExponentialMovingAverage(length, length);
            Assert.Equal(expected, batch.CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.HoltExponentialMovingAverage(prices, core, length);
            Assert.Equal(expected, core);
            Assert.Equal(expected, CalculationsHelper.GetMovingAverageList(Data(bars), MovingAvgType.HoltExponentialMovingAverage, length, prices.ToList()));
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), "Hema"), context);
            Assert.NotNull(actual); Assert.Equal(expected, actual.Value.ToArray());
            IStreamingIndicatorState state = new HoltExponentialMovingAverageState(length, length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { double.MaxValue })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }

    [Fact]
    public void IndependentPeriodsControlLevelAndTrend()
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, 1d, 0, 0, 0 };
        foreach (var alphaLength in new[] { 1, 3, 20, int.MaxValue })
        foreach (var gammaLength in new[] { 1, 7, 20, int.MaxValue })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.HoltOutputs(bars, alphaLength, gammaLength)["Hema"];
            Assert.Equal(expected, Data(bars).CalculateHoltExponentialMovingAverage(alphaLength, gammaLength).CustomValuesList);
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.MovingAverageCore.HoltExponentialMovingAverage(prices, core, alphaLength, gammaLength); Assert.Equal(expected, core);
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeHoltExponentialMovingAverageFast(Data(bars), context, alphaLength, gammaLength);
            Assert.Equal(expected, raw.Span.ToArray());
            var state = new HoltExponentialMovingAverageState(alphaLength, gammaLength);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < prices.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { 1d })[0]), false, true);
                    Assert.Equal(expected[i], state.Update(Native(bars[i]), false, true).Value);
                    Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
                }
            }
        }
        Assert.Equal(new[] { 1d, 2, 4 }, Data(BarsOf(new[] { 1d, 2, 4 })).CalculateHoltExponentialMovingAverage(1, 1).CustomValuesList);
        Assert.Equal(new[] { 1d, 1.5, 1.5 }, Data(BarsOf(new[] { 1d, 1, 1 })).CalculateHoltExponentialMovingAverage(3, 1).CustomValuesList);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new HoltExponentialMovingAverageState(3, 3);
            IStreamingIndicatorState control = new HoltExponentialMovingAverageState(3, 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(HoltExponentialMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.HoltOutputs(bars, ((HoltExponentialMovingAverageSpecOptions)((IBuiltInIndicator)testCase.Factory()).CreateOptions()).Length), IndicatorErrorBudget.Exact);
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
