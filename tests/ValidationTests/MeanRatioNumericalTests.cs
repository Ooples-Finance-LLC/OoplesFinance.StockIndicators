using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MeanRatioNumericalTests
{
    [Theory]
    [InlineData(1e-200)]
    [InlineData(1d)]
    [InlineData(double.MaxValue)]
    public void JapaneseRatioPreservesFiniteChangesAcrossOverflowingRanges(double magnitude)
    {
        var values = new[] { magnitude, magnitude, -magnitude, -magnitude, -magnitude };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, magnitude, -magnitude, v, 1)).ToArray();
        var expected = new[] { 0d, .5, 0, -.5, -1 };
        StockData Data() => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var indicator = new JapaneseCorrelationCoefficient(3);
        Assert.Single(BuiltInFormulaReferences.For(indicator)).Check(new IndicatorValidationContext("range-example", bars, new[] { expected }, 0));
        Assert.Equal(expected, Data().CalculateJapaneseCorrelationCoefficient(length: 3).CustomValuesList);
        var builtIn = (IBuiltInIndicator)indicator;
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var fast = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.TryComputeFast(Data(), spec, context);
        Assert.NotNull(fast);
        Assert.Equal(expected, fast.Value.ToArray());
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            for (var i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                var input = new OhlcvBar("RATIO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, true, true).Value);
            }
        }
    }

    [Fact]
    public void DifferenceRatioPreservesUnboundedSelectedInputsAndZeroRangeContract()
    {
        Assert.Equal(2, OoplesFinance.StockIndicators.Helpers.ExactDifferenceRatio.Of(2, 0, 1, 0));
        Assert.Equal(-2, OoplesFinance.StockIndicators.Helpers.ExactDifferenceRatio.Of(-2, 0, 1, 0));
        Assert.Equal(0, OoplesFinance.StockIndicators.Helpers.ExactDifferenceRatio.Of(2, 0, 1, 1));
    }

    [Fact]
    public async Task MayerOverflowHasIndependentRejectionEvidence()
    {
        var prices = new[] { -double.MaxValue, 1e-200, double.MaxValue };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(MayerMultiple),
            "cancellation-overflow", () => new MayerMultiple(3)), new()
        {
            AdditionalFixtures = new[] { new IndicatorValidationFixture("mayer-overflow", bars) }
        });
        report.ThrowIfInvalid();
        var evidence = Assert.Single(report.FixtureEvidence.Where(f => f.Name == "mayer-overflow"));
        Assert.Equal(2, evidence.OutputOverflowRejectionsChecked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RatioContractsRejectErasingTinyNonzeroOutputs(bool japanese)
    {
        IIndicator indicator = japanese ? new JapaneseCorrelationCoefficient(1) : new MayerMultiple(2);
        var values = japanese ? new[] { 1e-200, 1e-200 } : new[] { 1d, 1e-200 };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, 1, -1, v, 1)).ToArray();
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        Assert.Single(rules);
        Assert.Throws<InvalidOperationException>(() => rules[0].Check(new IndicatorValidationContext(
            "erased-tiny-ratio", bars, new[] { japanese ? new[] { 0d, 0d } : new[] { 1d, 0d } }, 0)));
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "JapaneseCorrelationCoefficient", "OceanIndicator", "MayerMultiple", "HybridConvolutionFilter"
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
