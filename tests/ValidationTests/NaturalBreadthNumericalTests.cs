using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class NaturalBreadthNumericalTests
{
    [Fact]
    public void SelectedPricesOutsideTheCandleRangeAreNotSilentlyClamped()
    {
        Assert.Equal(2, OoplesFinance.StockIndicators.Helpers.ExactRangePosition.Fraction(2, 0, 1));
        Assert.Equal(-1, OoplesFinance.StockIndicators.Helpers.ExactRangePosition.Fraction(-1, 0, 1));
        Assert.Equal(0, OoplesFinance.StockIndicators.Helpers.ExactRangePosition.Fraction(2, 1, 1));
    }

    [Theory]
    [InlineData(1d)]
    [InlineData(1e-200)]
    [InlineData(double.MaxValue)]
    public async Task NaturalStochasticPreservesRepresentablePositionsAcrossScale(double magnitude)
    {
        var bars = new[] { magnitude, 0, -magnitude / 2 }.Select((value, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, magnitude, -magnitude, value, 1)).ToArray();
        var expected = new[] { 100d, 0, -50 };
        var indicator = new NaturalStochasticIndicator(1, 1);
        Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("range-hand-example", bars, new[] { expected }, 0));
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        Assert.Equal(expected, data.CalculateNaturalStochasticIndicator(length: 1, smoothLength: 1).CustomValuesList);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, run[indicator].ToArray());
        using var state = new NaturalStochasticIndicatorState(length: 1, smoothLength: 1);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var input = new OhlcvBar("RANGE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            Assert.Equal(expected[i], state.Update(input, false, true).Value);
            Assert.Equal(expected[i], state.Update(input, true, true).Value);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "NaturalDirectionalCombo", "NaturalDirectionalIndex", "NaturalStochasticIndicator", "NaturalMarketMirror",
        "NaturalMarketRiver", "NaturalMarketCombo", "NaturalMarketSlope", "McClellanOscillator",
        "DecisionPointBreadthSwenlinTradingOscillator", "ZweigMarketBreadthIndicator", "TickLineMomentumOscillator"
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
