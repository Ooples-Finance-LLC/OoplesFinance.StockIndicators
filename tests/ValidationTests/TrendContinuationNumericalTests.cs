using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrendContinuationNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RESIDUAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ConsecutiveRunsResetOnTiesAndRecoverThroughRollingSums()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 5, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { 0d, double.MaxValue, -double.MaxValue, 0, 0, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, double.Epsilon, 3 * double.Epsilon, 0, 0, 0 } })
        foreach (var period in new[] { 1, 2, 3, 7 })
        {
            IBuiltInIndicator indicator = new TrendContinuationFactor(period);
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.TrendContinuationOutputs(bars, indicator);
            var batch = Data(bars).CalculateTrendContinuationFactor(period);
            foreach (var key in new[] { "TcfPlus", "TcfMinus" })
            {
                Assert.Equal(expected[key], batch.OutputValues[key]);
                using var context = new ComputeContext();
                using var raw = IndicatorCompute.ComputeArm(Data(bars), new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
                var selected = Data(bars.Select(b => new Bar(b.Time, b.Open, b.High, b.Low, 42, b.Volume)).ToArray()); selected.CustomValuesList = prices.ToList();
                using var selectedArm = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), key), context);
                Assert.NotNull(selectedArm); Assert.Equal(expected[key], selectedArm.Value.ToArray());
                Assert.Equal(expected[key], selected.CalculateTrendContinuationFactor(period).OutputValues[key]);
                if (key == "TcfPlus")
                {
                    var core = new double[bars.Length];
                    OoplesFinance.StockIndicators.Core.OscillatorCore.TrendContinuationFactor(prices, core, period); Assert.Equal(expected[key], core);
                }
            }
            using var state = new TrendContinuationFactorState(period);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Update(Native(BarsOf(new[] { 0d })[0]), true, true);
                state.Update(Native(BarsOf(new[] { double.MaxValue })[0]), true, true); state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    state.Update(Native(BarsOf(new[] { -double.MaxValue })[0]), false, true);
                    foreach (var final in new[] { false, false, true })
                    {
                        var actual = state.Update(Native(bars[i]), final, true);
                        foreach (var key in new[] { "TcfPlus", "TcfMinus" }) Assert.Equal(expected[key][i], actual.Outputs![key]);
                    }
                }
            }
        }
        var recovery = Data(BarsOf(new[] { 0d, double.MaxValue, -double.MaxValue, 0, 0, 0 })).CalculateTrendContinuationFactor(2);
        Assert.Equal(new[] { 0d, double.MaxValue, -double.MaxValue, -double.MaxValue, double.MaxValue, 0 }, recovery.OutputValues["TcfPlus"]);
        Assert.Equal(new[] { 0d, -double.MaxValue, double.MaxValue, double.MaxValue, -double.MaxValue, 0 }, recovery.OutputValues["TcfMinus"]);
        var simple = Data(BarsOf(new[] { 1d, 3, 6, 6, 4, 1 })).CalculateTrendContinuationFactor(3);
        Assert.Equal(new[] { 0d, 2, 5, 5, 1, -7 }, simple.OutputValues["TcfPlus"]);
        Assert.Equal(new[] { 0d, -2, -7, -7, -3, 5 }, simple.OutputValues["TcfMinus"]);
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new TrendContinuationFactorState(3); using var control = new TrendContinuationFactorState(3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RESIDUAL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value); Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "TrendContinuationFactor" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.TrendContinuationOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);

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
