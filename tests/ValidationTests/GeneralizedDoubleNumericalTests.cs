using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class GeneralizedDoubleNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void ExactAffineCombinationRetainsUnitGainAndExtremeCancellation()
    {
        foreach (var prices in new[] {
            new[] { 2d, 5, 0, 3, 8, -3, 1, -5, 0, 0, 0, 0 },
            new[] { double.MaxValue, -double.MaxValue, double.MaxValue / 2, 0, 0, 0 },
            new[] { double.Epsilon, 0d, -double.Epsilon, 3 * double.Epsilon, 0, 0 },
            Enumerable.Repeat(double.MaxValue, 10).ToArray() })
        foreach (var length in new[] { 1, 2, 3, 7 })
        foreach (var factor in new[] { 0d, .7, 1, -1, double.MaxValue, -double.MaxValue })
        {
            var bars = BarsOf(prices); IBuiltInIndicator indicator = new GeneralizedDoubleExponentialMovingAverage(length, factor);
            var expected = BuiltInFormulaReferences.GeneralizedDoubleOutputs(bars, indicator)["Gdema"];
            Assert.Equal(expected, Data(bars).CalculateGeneralizedDoubleExponentialMovingAverage(length: length, factor: factor).CustomValuesList);
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(prices, core, length, factor); Assert.Equal(expected, core);
            var selected = Data(BarsOf(prices.Select(_ => 42d).ToArray())); selected.CustomValuesList = prices.ToList();
            using var context = new ComputeContext();
            using var actual = IndicatorCompute.ComputeArm(selected, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), "Gdema"), context);
            Assert.NotNull(actual); Assert.Equal(expected, actual.Value.ToArray());
            using var state = new GeneralizedDoubleExponentialMovingAverageState(length: length, factor: factor);
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
        foreach (var factor in new[] { .7, double.MaxValue, -double.MaxValue })
            Assert.All(Data(BarsOf(Enumerable.Repeat(double.MaxValue, 10).ToArray())).CalculateGeneralizedDoubleExponentialMovingAverage(length: 1, factor: factor).CustomValuesList, value => Assert.Equal(double.MaxValue, value));
        var ordinary = new[] { 2d, 4, 6 };
        Assert.Equal(new[] { 4d / 3, 10d / 3, 16d / 3 }, Data(BarsOf(ordinary)).CalculateGeneralizedDoubleExponentialMovingAverage(MovingAvgType.WeightedMovingAverage, 2, 0).CustomValuesList);
        var defaultCore = new double[ordinary.Length];
        OoplesFinance.StockIndicators.Core.MovingAverageCore.GeneralizedDoubleExponentialMovingAverage(ordinary, defaultCore, 2);
        Assert.Equal(Data(BarsOf(ordinary)).CalculateGeneralizedDoubleExponentialMovingAverage(length: 2).CustomValuesList, defaultCore);
        Assert.Equal(defaultCore, CalculationsHelper.GetMovingAverageList(Data(BarsOf(ordinary)), MovingAvgType.GeneralizedDoubleExponentialMovingAverage, 2, ordinary.ToList()));
    }

    [Fact]
    public void CustomerStagesRemainSequentialAndPreserveTheFactor()
    {
        var bars = BarsOf(new[] { 2d, 5, 3, 8, 1 });
        var callbacks = new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
            (values, period) => { Assert.Equal(3, period); Assert.Equal(bars.Select(b => b.Close), values); return values.Select(_ => 5d).ToArray(); },
            (values, period) => { Assert.Equal(3, period); Assert.All(values, value => Assert.Equal(5, value)); return values.Select(_ => 9d).ToArray(); } };
        using var armed = ComponentAverage.Arm(callbacks); using var context = new ComputeContext();
        using var actual = IndicatorCompute.ComputeGeneralizedDoubleEmaFast(Data(bars), context, 3, factor: .5);
        Assert.Equal(2, ComponentAverage.Substitutions); Assert.All(actual.Span.ToArray(), value => Assert.Equal(3, value));
    }

    [Fact]
    public void InvalidFactorsRejectBeforeStateAllocation()
    {
        foreach (var factor in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeneralizedDoubleExponentialMovingAverageState(factor: factor));
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(BarsOf(new[] { 1d })).CalculateGeneralizedDoubleExponentialMovingAverage(factor: factor));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeGeneralizedDoubleEmaFast(Data(BarsOf(new[] { 1d })), context, factor: factor));
        }
    }

    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new GeneralizedDoubleExponentialMovingAverageState(length: 3); using var control = new GeneralizedDoubleExponentialMovingAverageState(length: 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("GD", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(GeneralizedDoubleEma) || c.IndicatorType == typeof(GeneralizedDoubleExponentialMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.GeneralizedDoubleOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
