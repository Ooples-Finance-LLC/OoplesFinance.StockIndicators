using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ParametricCorrectiveNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void SignedLaggedWeightsPreservePreviewResetAndExtendedRange()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 1063, int.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, double.Epsilon, -double.Epsilon, 3 * double.Epsilon, 0, 0 }, Enumerable.Repeat(double.MaxValue, 90).ToArray() })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new ParametricCorrectiveLinearMovingAverage(length);
            var expected = BuiltInFormulaReferences.ParametricCorrectiveOutputs(bars, indicator)["Pclma"];
            Assert.Equal(expected, Data(bars).CalculateParametricCorrectiveLinearMovingAverage(length).CustomValuesList);
            var core = new double[prices.Length]; OoplesFinance.StockIndicators.Core.MovingAverageCore.ParametricCorrectiveLinearMovingAverage(prices, core, length); Assert.Equal(expected, core);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeParametricCorrectiveLinearMovingAverageFast(Data(bars), context, length); Assert.Equal(expected, raw.Span.ToArray());
            var state = new ParametricCorrectiveLinearMovingAverageState(length);
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
    public void RawAndBatchRespectSelectedInputBeforeCloses()
    {
        var original = BarsOf(new[] { 20d, 40, 30, 80 }); var selected = new[] { 1d, 3, -2, 4 };
        var expected = BuiltInFormulaReferences.ParametricCorrectiveOutputs(BarsOf(selected), new ParametricCorrectiveLinearMovingAverage(3))["Pclma"];
        var data = Data(original); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeParametricCorrectiveLinearMovingAverageFast(data, context, 3);
        Assert.Equal(expected, raw.Span.ToArray());
        Assert.Equal(expected, data.CalculateParametricCorrectiveLinearMovingAverage(3).CustomValuesList);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = new ParametricCorrectiveLinearMovingAverageState(3);
            IStreamingIndicatorState control = new ParametricCorrectiveLinearMovingAverageState(3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    [Fact]
    public void SignedWeightsAndZeroMassUseExactQuotients()
    {
        foreach (var length in new[] { 1, 2, 7 })
        foreach (var alpha in new[] { 0d, -.5, 1, 2, double.MaxValue })
        foreach (var per in new[] { -35d, 0, 35, 75, 175, double.MaxValue, -double.MaxValue })
        {
            var bars = BarsOf(new[] { 2d, -7, 3, 8, -1, 6, 4, 9, 2, -5, 1, 7 });
            var expected = BuiltInFormulaReferences.ParametricCorrectiveOutputs(bars, new ParametricCorrectiveLinearMovingAverage(length), alpha, per)["Pclma"];
            Assert.Equal(expected, Data(bars).CalculateParametricCorrectiveLinearMovingAverage(length, alpha, per).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeParametricCorrectiveLinearMovingAverageFast(Data(bars), context, length, alpha, per);
            Assert.Equal(expected, raw.Span.ToArray());
            using var state = new ParametricCorrectiveLinearMovingAverageState(length, alpha, per);
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(BarsOf(new[] { -99d })[0]), false, true);
                Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
            }
        }
    }

    [Fact]
    public void NonfiniteWeightsAreRejectedBeforeEmptyInput()
    {
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ParametricCorrectiveLinearMovingAverageState(2, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ParametricCorrectiveLinearMovingAverageState(2, per: invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateParametricCorrectiveLinearMovingAverage(2, invalid));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateParametricCorrectiveLinearMovingAverage(2, per: invalid));
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ParametricCorrectiveLinearMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ParametricCorrectiveOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
