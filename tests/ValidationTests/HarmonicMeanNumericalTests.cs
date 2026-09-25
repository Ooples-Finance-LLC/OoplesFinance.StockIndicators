using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class HarmonicMeanNumericalTests
{
    public static IEnumerable<object[]> HandExamples => new[]
    {
        new object[] { new[] { double.Epsilon, double.Epsilon, double.Epsilon }, double.Epsilon },
        new object[] { new[] { double.MaxValue, double.MaxValue, double.MaxValue }, double.MaxValue },
        new object[] { new[] { 1d, -1, double.Epsilon }, 3 * double.Epsilon },
        new object[] { new[] { double.MaxValue, -double.MaxValue, 1d }, 3d },
        new object[] { new[] { 1d, 0, 3 }, 1.5 },
        new object[] { new[] { 1d, -1, 0 }, 0d },
        new object[] { new[] { 1d, -1, 3 }, 9d },
        new object[] { new[] { 1d, -1, -3 }, -9d },
        new object[] { new[] { 2d, 4, 8 }, 24d / 7 },
        new object[] { new[] { double.MaxValue, double.MaxValue, -double.MaxValue }, double.PositiveInfinity }
    };

    [Theory, MemberData(nameof(HandExamples))]
    public void ExactReciprocalsPreserveCancellationAndZeroConventions(double[] prices, double last)
    {
        var expected = new[] { prices[0], prices[1], last };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        StockData Data(bool selected = false)
        {
            var raw = selected ? new[] { 100d, 100, 100 } : prices;
            var data = new StockData(raw, raw, raw, raw, new[] { 1d, 1, 1 }, bars.Select(b => b.Time));
            if (selected) data.CustomValuesList = prices.ToList();
            return data;
        }
        Assert.Equal(expected, BuiltInFormulaReferences.HarmonicMeanReference(bars, 3));
        Assert.Equal(expected, Data().CalculateHarmonicMeanMovingAverage(3).CustomValuesList);
        var indicator = new HarmonicMeanMovingAverage(3);
        var builtIn = (IBuiltInIndicator)indicator;
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
        foreach (var selected in new[] { false, true })
        {
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var fast = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.TryComputeFast(Data(selected), spec, context);
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(Data(selected), spec, context);
            Assert.NotNull(fast);
            Assert.NotNull(arm);
            Assert.Equal(expected, fast.Value.ToArray());
            Assert.Equal(expected, arm.Value.ToArray());
        }
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var input = new OhlcvBar("HARMONIC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(input, false, true).Value);
                    Assert.Equal(expected[i], state.Update(input, true, true).Value);
                }
            }
        }
    }

    [Fact]
    public async Task PublishedOverflowHasIndependentRejectionEvidence()
    {
        var prices = new[] { double.MaxValue, double.MaxValue, -double.MaxValue };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(HarmonicMeanMovingAverage),
            "harmonic-overflow", () => new HarmonicMeanMovingAverage(3)), new()
        {
            AdditionalFixtures = new[] { new IndicatorValidationFixture("proven-harmonic-overflow", bars) }
        });
        report.ThrowIfInvalid();
        Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == "proven-harmonic-overflow").OutputOverflowRejectionsChecked);
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "HarmonicMeanMovingAverage"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => new Dictionary<string, double[]>
        { ["Hmma"] = BuiltInFormulaReferences.HarmonicMeanReference(bars, ((OoplesFinance.StockIndicators.Builder.Specs.HarmonicMeanMovingAverageSpecOptions)((IBuiltInIndicator)testCase.Factory()).CreateOptions()).Length) });

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
