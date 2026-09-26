using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AdaptiveAutonomousNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("GD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void EfficiencyStartupAndExtendedBandsPreserveEveryRoute()
    {
        foreach (var length in new[] { 1, 2, 3, 7, 20 })
        foreach (var gamma in new[] { 0d, 1, 3, double.MaxValue })
        foreach (var prices in new[] { Array.Empty<double>(), new[] { 1d, 2, 4, 2, 3, -1, 0, 0, 0 }, new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, 0, 0, 0 }, new[] { double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 0 } })
        {
            var bars = BarsOf(prices); var expected = BuiltInFormulaReferences.AdaptiveAutonomousOutputs(bars, length, gamma, false);
            var stop = BuiltInFormulaReferences.AdaptiveAutonomousOutputs(bars, length, gamma, true)["Ts"];
            var batch = Data(bars).CalculateAdaptiveAutonomousRecursiveMovingAverage(length, gamma);
            foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
            Assert.Equal(stop, Data(bars).CalculateAdaptiveAutonomousRecursiveTrailingStop(length, gamma).CustomValuesList);
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeAdaptiveAutonomousRecursiveMovingAverageFast(Data(bars), context, length, gamma);
            using var deviation = IndicatorCompute.ComputeAdaptiveAutonomousRecursiveMovingAverageFast(Data(bars), context, length, gamma, true);
            using var rawStop = IndicatorCompute.ComputeAdaptiveAutonomousRecursiveTrailingStopFast(Data(bars), context, length, gamma);
            Assert.Equal(expected["Aarma"], raw.Span.ToArray()); Assert.Equal(expected["D"], deviation.Span.ToArray()); Assert.Equal(stop, rawStop.Span.ToArray());
            var core = new double[prices.Length];
            OoplesFinance.StockIndicators.Core.TrendCore.AdaptiveAutonomousRecursiveMovingAverage(prices, core, length, gamma); Assert.Equal(expected["Aarma"], core);
            OoplesFinance.StockIndicators.Core.MovingAverageCore.AdaptiveAutonomousRecursiveTrailingStop(prices, prices, prices, core, length, gamma); Assert.Equal(stop, core);
            if (gamma == 3) { OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.Get(MovingAvgType.AdaptiveAutonomousRecursiveMovingAverage)!.Compute(prices, core, length); Assert.Equal(expected["Aarma"], core); }
            using var averageState = new AdaptiveAutonomousRecursiveMovingAverageState(length, gamma);
            using var stopState = new AdaptiveAutonomousRecursiveTrailingStopState(length, gamma);
            for (var replay = 0; replay < 2; replay++)
            {
                averageState.Reset(); stopState.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var final in new[] { false, false, true })
                {
                    var actual = averageState.Update(Native(bars[i]), final, true);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                    Assert.Equal(stop[i], stopState.Update(Native(bars[i]), final, true).Value);
                }
            }
        }
    }
    [Fact]
    public void SelectedPricesAndHandCalculatedStartupArePreserved()
    {
        var data = Data(BarsOf(new[] { 9d, 9, 9 })); data.SetCustomValues(new List<double> { 1, 2, 4 });
        using var context = new ComputeContext();
        using var average = IndicatorCompute.ComputeAdaptiveAutonomousRecursiveMovingAverageFast(data, context, 2, 1);
        using var stop = IndicatorCompute.ComputeAdaptiveAutonomousRecursiveTrailingStopFast(data, context, 2, 1);
        Assert.Equal(new[] { 1d, 1, 6 }, average.Span.ToArray()); Assert.Equal(new[] { 1d, 0, 4 }, stop.Span.ToArray());
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAnyState()
    {
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        foreach (var stop in new[] { false, true })
        {
            IStreamingIndicatorState state = stop ? new AdaptiveAutonomousRecursiveTrailingStopState(3) : new AdaptiveAutonomousRecursiveMovingAverageState(3);
            IStreamingIndicatorState control = stop ? new AdaptiveAutonomousRecursiveTrailingStopState(3) : new AdaptiveAutonomousRecursiveMovingAverageState(3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("AA", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(AdaptiveAutonomousRecursiveMovingAverage) || c.IndicatorType == typeof(AdaptiveAutonomousRecursiveTrailingStop)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AdaptiveAutonomousOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
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
