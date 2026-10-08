using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EllipticNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void RecurrencesPreserveStartupCancellationPreviewAndReset()
    {
        foreach (var modified in new[] { false, true })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 0, 0, 0, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 },
            Enumerable.Repeat(double.MaxValue, 90).ToArray(), Enumerable.Range(0, 90).Select(i => (double)(i % 11 - 5)).ToArray() })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.EllipticNumericalOutputs(bars, modified).Values.Single();
            var batch = modified ? Data(bars).CalculateEhlersModifiedOptimumEllipticFilter() : Data(bars).CalculateEhlersOptimumEllipticFilter();
            Assert.Equal(expected, batch.CustomValuesList);
            var core = new double[prices.Length];
            if (modified) OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersModifiedOptimumEllipticFilter(prices, core);
            else OoplesFinance.StockIndicators.Core.MovingAverageCore.EhlersOptimumEllipticFilter(prices, core);
            Assert.Equal(expected, core);
            if (modified) Assert.Equal(expected, CalculationsHelper.GetMovingAverageList(Data(bars), MovingAvgType.EhlersModifiedOptimumEllipticFilter, 14, prices.ToList()));
            IStreamingIndicatorState state = modified ? new EhlersModifiedOptimumEllipticFilterState() : new EhlersOptimumEllipticFilterState();
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { 99d })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var modified in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = modified ? new EhlersModifiedOptimumEllipticFilterState() : new EhlersOptimumEllipticFilterState();
            IStreamingIndicatorState control = modified ? new EhlersModifiedOptimumEllipticFilterState() : new EhlersOptimumEllipticFilterState();
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("BC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(EhlersOptimumEllipticFilter) || c.IndicatorType == typeof(EhlersModifiedOptimumEllipticFilter)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.EllipticNumericalOutputs(bars, ((IBuiltInIndicator)testCase.Factory()).BatchName == IndicatorName.EhlersModifiedOptimumEllipticFilter), IndicatorErrorBudget.Exact);
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
