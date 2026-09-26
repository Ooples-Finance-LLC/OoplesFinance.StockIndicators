using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class HistoricalPercentileNumericalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void RanksPreserveExtremeReturnsAndSameSignNegativePrices(bool extreme, bool negative)
    {
        var small = extreme ? double.Epsilon : 1d;
        var large = extreme ? double.MaxValue : 2d;
        var values = new[] { small, large, small, large }.Select(v => negative ? -v : v).ToArray();
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = new[] { 0d, 100d / 3, 200d / 3, 200d / 3 };
        StockData Data() => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var indicator = new HistoricalVolatilityPercentile(length: 2, annualLength: 3);
        var builtIn = (IBuiltInIndicator)indicator;
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
        var batch = Data().CalculateHistoricalVolatilityPercentile(length: 2, annualLength: 3);
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], batch.CustomValuesList[i], 12);
        foreach (var rule in BuiltInFormulaReferences.For(indicator))
            rule.Check(new IndicatorValidationContext("extreme-log-ranks", bars,
                new[] { expected, batch.OutputValues["Signal"].ToArray() }, 0));
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var fast = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.TryComputeFast(Data(), spec, context);
        Assert.NotNull(fast);
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], fast.Value.ToArray()[i], 12);
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            for (var i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                var input = new OhlcvBar("RANK", BarTimeframe.Minutes(1), bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
                Assert.Equal(expected[i], state.Update(input, false, true).Value, 12);
                Assert.Equal(expected[i], state.Update(input, true, true).Value, 12);
            }
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "HistoricalVolatilityPercentile"
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
