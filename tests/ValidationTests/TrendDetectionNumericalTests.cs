using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrendDetectionNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactRollingMomentumTotalsRecoverAfterExtremeCancellation()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { 0d, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 } })
        foreach (var shortPeriod in new[] { 1, 2, 5 })
        foreach (var longPeriod in new[] { 1, 3, 7 })
        {
            IBuiltInIndicator indicator = new TrendDetectionIndex(shortPeriod, longPeriod);
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.TrendDetectionOutputs(bars, indicator, longPeriod);
            var batch = Data(bars).CalculateTrendDetectionIndex(shortPeriod, longPeriod);
            foreach (var key in new[] { "Tdi", "TdiDirection" })
            {
                Assert.Equal(expected[key], batch.OutputValues[key]);
                using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeTrendDetectionIndexFast(Data(bars), context, shortPeriod, longPeriod, key == "TdiDirection");
                Assert.Equal(expected[key], raw.Span.ToArray());
                var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
                using var selectedArm = IndicatorCompute.ComputeTrendDetectionIndexFast(selected, context, shortPeriod, longPeriod, key == "TdiDirection");
                Assert.Equal(expected[key], selectedArm.Span.ToArray());
                Assert.Equal(expected[key], selected.CalculateTrendDetectionIndex(shortPeriod, longPeriod).OutputValues[key]);
                var core = new double[bars.Length];
                OoplesFinance.StockIndicators.Core.OscillatorCore.TrendDetectionIndex(prices, core, shortPeriod, longPeriod, key == "TdiDirection"); Assert.Equal(expected[key], core);
            }
            using var state = new TrendDetectionIndexState(shortPeriod, longPeriod);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { double.MaxValue })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var key in new[] { "Tdi", "TdiDirection" }) Assert.Equal(expected[key][i], actual.Outputs![key]);
                    }
                }
            }
        }
        var recovery = Data(BarsOf(new[] { 0d, double.MaxValue, -double.MaxValue, 0, 0, 0 })).CalculateTrendDetectionIndex(1, 2);
        Assert.Equal(new[] { 0d, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0 }, recovery.CustomValuesList);
        Assert.True(double.IsNegativeInfinity(recovery.OutputValues["TdiDirection"][2]));
        Assert.Equal(new[] { 0d, 1, 0, 0 }, Data(BarsOf(new[] { 1d, 2, 3, 4 })).CalculateTrendDetectionIndex(1, 2).CustomValuesList);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new TrendDetectionIndexState(2, 3); using var control = new TrendDetectionIndexState(2, 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "TrendDetection", "TrendDetectionIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.TrendDetectionOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
