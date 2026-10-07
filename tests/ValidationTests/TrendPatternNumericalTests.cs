using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrendPatternNumericalTests
{
    [Fact]
    public void MinimumPeriodExhaustionUsesTheTwoBarBreakoutWindow()
    {
        var highs = new[] { 10d, 9, 9.5 };
        var expected = new[] { 1d, .5, 1d / 3 };
        var bars = highs.Select((high, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), i + 1, high, 0, i + 1, 1)).ToArray();
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var batch = data.CalculateTrendExhaustionIndicator(length: 1);
        using var state = new TrendExhaustionIndicatorState(length: 1);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var input = new OhlcvBar("EXHAUSTION", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            Assert.Equal(expected[i], batch.OutputValues["Tei"][i], 14);
            Assert.Equal(expected[i], state.Update(input, false, true).Value, 14);
            Assert.Equal(expected[i], state.Update(input, true, true).Value, 14);
        }
    }

    [Theory]
    [InlineData(8d, 0d)]
    [InlineData(0d, 90d)]
    public void VostroFactoriesPreserveTheConfiguredThreshold(double level, double expected)
    {
        // At bar two the normalized upper price is 0.25, between the two thresholds.
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(
            OoplesFinance.StockIndicators.Enums.IndicatorName.VostroIndicator,
            new OoplesFinance.StockIndicators.Builder.Specs.VostroIndicatorSpecOptions(5, 100, level));
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            var first = new OhlcvBar("VOSTRO", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch,
                -1, -1, -100, -1, 1, true);
            var second = new OhlcvBar("VOSTRO", BarTimeframe.Minutes(1), DateTime.UnixEpoch.AddMinutes(1), DateTime.UnixEpoch.AddMinutes(1),
                1, 1, 0, 1, 1, true);
            Assert.Equal(-90, state.Update(first, true, true).Value);
            Assert.Equal(expected, state.Update(second, false, true).Value);
            Assert.Equal(expected, state.Update(second, true, true).Value);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "ElderImpulseSystem", "GannHiLoActivator", "LinearChannels", "LinearTrailingStop", "SupportResistance", "TimePriceIndicator", "TrendExhaustionIndicator", "VanillaABCDPattern", "VixTradingSystem", "VostroIndicator", "UhlMaCrossoverSystem", "RelativeDifferenceOfSquaresOscillator"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(testCase, route);

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
