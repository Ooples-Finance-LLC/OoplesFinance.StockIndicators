using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WilderSummationNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SUM", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void RecurrenceStartsAtFirstPriceAndRecoversAfterOverflow()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 0 },
            new[] { double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0 },
            new[] { double.Epsilon, double.Epsilon, -double.Epsilon, 3 * double.Epsilon, 0, 0 } })
        foreach (var length in new[] { 1, 2, 3, 7 })
        {
            var bars = BarsOf(prices);
            IBuiltInIndicator indicator = new WellesWilderSummation(length);
            var expected = BuiltInFormulaReferences.WilderSummationOutputs(bars, indicator)["Wws"];
            Assert.Equal(expected, Data(bars).CalculateWellesWilderSummation(length).CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.WellesWilderSummation(prices, core, length); Assert.Equal(expected, core);
            OoplesFinance.StockIndicators.Core.MovingAverageCore.WildersSummationMethod(prices, core, length); Assert.Equal(expected, core);
            foreach (IBuiltInIndicator alias in new IBuiltInIndicator[] { indicator, new WildersSummationMethod(length) })
            {
                var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
                using var context = new ComputeContext();
                using var actual = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(alias.BatchName, alias.CreateOptions(), "Wws"), context);
                Assert.NotNull(actual); Assert.Equal(expected, actual.Value.ToArray());
            }
            var state = new WellesWilderSummationState(length);
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
        Assert.Equal(new[] { 2d, 6, 6, 3 }, Data(BarsOf(new[] { 2d, 5, 3, 0 })).CalculateWellesWilderSummation(2).CustomValuesList);
        var recovery = Data(BarsOf(new[] { double.MaxValue, double.MaxValue, -double.MaxValue })).CalculateWellesWilderSummation(2).CustomValuesList;
        Assert.True(double.IsPositiveInfinity(recovery[1])); Assert.True(double.IsFinite(recovery[2])); Assert.True(recovery[2] < 0);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = new WellesWilderSummationState(3); var control = new WellesWilderSummationState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("SUM", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "WellesWilderSummation", "WildersSummationMethod" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.WilderSummationOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
