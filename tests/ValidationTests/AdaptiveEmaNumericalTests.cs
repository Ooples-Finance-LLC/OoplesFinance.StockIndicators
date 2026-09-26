using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AdaptiveEmaNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void OhlcRangesStartupAndExtendedFeedbackMatchEveryRoute()
    {
        foreach (var length in new[] { 1, 2, 3, 7 })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var bars = new[] { (2d, 4d, 1d, 3d), (4d, 8d, 2d, 7d), (6d, 7d, 1d, 2d), (2d, 12d, -4d, 3d), (3d, 3d, 3d, 3d), (-3d, -2d, -4d, -3d), (1d, 1d, -1d, 12d), (1d, 1d, -1d, -12d), (1d, 1d, -1d, 12d) }
                .Select((b, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), b.Item1 * scale, b.Item2 * scale, b.Item3 * scale, b.Item4 * scale, 1)).ToArray();
            var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
            var expected = BuiltInFormulaReferences.AdaptiveEmaOutputs(bars, length, code)["Aema"];
            Assert.Equal(expected, Data(bars).CalculateAdaptiveExponentialMovingAverage(kind, length).CustomValuesList);
            using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAdaptiveExponentialMovingAverageFast(Data(bars), context, length, kind);
            using var alias = IndicatorCompute.ComputeAdaptiveEmaFast(Data(bars), context, length, kind);
            Assert.Equal(expected, raw.Span.ToArray()); Assert.Equal(expected, alias.Span.ToArray());
            if (code == 1)
            {
                var core = new double[bars.Length]; var registry = OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.Get(MovingAvgType.AdaptiveExponentialMovingAverage)!;
                Assert.True(registry.RequiresOhlc);
                registry.ComputeOhlc(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length); Assert.Equal(expected, core);
                Assert.Equal(expected, CalculationsHelper.GetMovingAverageList(Data(bars), MovingAvgType.AdaptiveExponentialMovingAverage, length));
            }
            using var state = new AdaptiveExponentialMovingAverageState(kind, length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true }) Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Value);
            }
        }
    }
    [Fact]
    public void CustomerSeedIsUsedThroughTheInclusiveStartupBoundary()
    {
        var prices = new[] { 1d, 2, 4 }; var data = Data(BarsOf(new[] { 9d, 9, 9 })); data.SetCustomValues(prices.ToList());
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeAdaptiveEmaFast(data, context, 2);
        Assert.Equal(new[] { 0d, 1.5, 3 }, raw.Span.ToArray());
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(2, period); Assert.Equal(prices, values); return new[] { 3d, 6, 9 }; } });
            if (batch) Assert.Equal(new[] { 3d, 6, 9 }, Data(BarsOf(prices)).CalculateAdaptiveExponentialMovingAverage(length: 2).CustomValuesList);
            else { using var value = IndicatorCompute.ComputeAdaptiveExponentialMovingAverageFast(Data(BarsOf(prices)), context, 2); Assert.Equal(new[] { 3d, 6, 9 }, value.Span.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void EmptyAndInvalidInputsDoNotAdvanceState()
    {
        Assert.Empty(Data(Array.Empty<Bar>()).CalculateAdaptiveExponentialMovingAverage().CustomValuesList);
        var empty = Array.Empty<double>(); OoplesFinance.StockIndicators.Core.MovingAverageCore.AdaptiveExponentialMovingAverage(empty, empty);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            using var state = new AdaptiveExponentialMovingAverageState(length: 3); using var control = new AdaptiveExponentialMovingAverageState(length: 3);
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("AE", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AdaptiveEma) || c.IndicatorType == typeof(AdaptiveExponentialMovingAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AdaptiveEmaOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
