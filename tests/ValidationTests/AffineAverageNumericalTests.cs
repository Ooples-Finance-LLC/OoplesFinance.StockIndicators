using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AffineAverageNumericalTests
{
    public static IEnumerable<object[]> HandExamples => new[]
    {
        new object[] { false, 3, new[] { 1d, 2, 4 }, new[] { 1d / 6, 2d / 3, 11d / 6 } },
        new object[] { false, 7, new[] { 1d, 2, 4 }, new[] { 0d, 0, 0 } },
        new object[] { true, 2, new[] { 1d, 2, 4 }, new[] { .5, 2, 4 } },
        new object[] { true, 2, new[] { double.MaxValue, double.MaxValue }, new[] { double.MaxValue / 2, double.MaxValue } },
        new object[] { true, 2, new[] { -double.MaxValue, double.MaxValue }, new[] { -double.MaxValue / 2, double.MaxValue } },
        new object[] { true, 2, new[] { double.Epsilon, double.Epsilon }, new[] { 0d, double.Epsilon } }
    };

    [Theory, MemberData(nameof(HandExamples))]
    public void SharedMovingAverageRoutesPreservePublishedWeights(bool sharp, int length, double[] prices, double[] expected)
    {
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = sharp ? (IBuiltInIndicator)new SharpModifiedMovingAverage(length) : new EndPointMovingAverage(length);
        Assert.Equal(expected, BuiltInFormulaReferences.AffineAverageOutputs(bars, indicator).Values.Single());
        var type = sharp ? MovingAvgType.SharpModifiedMovingAverage : MovingAvgType.EndPointWeightedMovingAverage;
        var output = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.GetRequired(type).Compute(prices, output, length);
        Assert.Equal(expected, output);
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        Assert.Equal(expected, OoplesFinance.StockIndicators.Helpers.CalculationsHelper.GetMovingAverageList(data, type, length));
        data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        if (sharp) data.CalculateSharpModifiedMovingAverage(length: length);
        else data.CalculateEndPointMovingAverage(length: length);
        Assert.Equal(expected, data.CustomValuesList);
        var raw = prices.Select(_ => 100d).ToArray();
        var selected = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), bars.Select(b => b.Time));
        selected.CustomValuesList = prices.ToList();
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(selected, spec, context);
        Assert.NotNull(arm);
        Assert.Equal(expected, arm.Value.ToArray());
    }

    [Fact]
    public void EmptyAndMaximumPeriodCoreCallsDoNotAllocatePeriodSizedBuffers()
    {
        foreach (var type in new[] { MovingAvgType.EndPointWeightedMovingAverage, MovingAvgType.SharpModifiedMovingAverage })
        {
            var core = OoplesFinance.StockIndicators.Core.Registry.MovingAverageRegistry.GetRequired(type);
            core.Compute(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
            var output = new double[1];
            core.Compute(new[] { 1d }, output, int.MaxValue);
            Assert.True(double.IsFinite(output[0]));
        }
    }

    [Fact]
    public async Task UnboundedSignedAverageHasIndependentOverflowRejectionEvidence()
    {
        var prices = new[] { double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue,
            -double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        Assert.Equal(double.NegativeInfinity,
            BuiltInFormulaReferences.AffineAverageOutputs(bars, new EndPointMovingAverage(8))["Epma"][7]);
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(EndPointMovingAverage),
            "endpoint-overflow", () => new EndPointMovingAverage(8)), new()
        {
            AdditionalFixtures = new[] { new IndicatorValidationFixture("proven-endpoint-overflow", bars) }
        });
        report.ThrowIfInvalid();
        Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == "proven-endpoint-overflow").OutputOverflowRejectionsChecked);
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "EndPointMovingAverage", "SharpModifiedMovingAverage"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route,
            bars => BuiltInFormulaReferences.AffineAverageOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
