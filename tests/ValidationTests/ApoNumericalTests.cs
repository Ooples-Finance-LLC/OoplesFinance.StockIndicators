using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ApoNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Apo) || c.IndicatorType == typeof(AbsolutePriceOscillator) || c.IndicatorType == typeof(PriceOscillator))
        .Select(c => new object[] { c });

    [Fact]
    public void DiscoveryIncludesEveryPromotedComposition()
        => Assert.Equal(72, Cases.Count(row => ((IndicatorValidationCase)row[0]).Name.StartsWith("apo-composition/")));

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void NativeFactoriesMatchIndependentReferenceAndPreserveLifecycle(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var reference = Assert.Single(BuiltInFormulaReferences.For(indicator)).OverflowReference!;
        Assert.NotNull(reference);
        var spec = new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
            {
                var bars = fixture.Bars;
                var expected = reference(bars);
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("APO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Apo"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SubnormalDifferenceSurvivesAndTrueOverflowIsRejected()
    {
        Bar[] BarsFor(double[] values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var small = BarsFor(new[] { 0d, double.Epsilon, 3 * double.Epsilon });
        var indicator = new Apo(1, 3);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(small)).ConfigureIndicators(indicator).BuildAsync();
        // EMA seeds from the partial mean: at bar two RN(4*epsilon/3) = epsilon.
        Assert.Equal(new[] { 0d, double.Epsilon, 2 * double.Epsilon }, run[indicator].ToArray());
        var overflow = BarsFor(new[] { -double.MaxValue, -double.MaxValue, double.MaxValue });
        Assert.Contains(BuiltInFormulaReferences.RoundedApo(overflow, 1, 3, 1), double.IsInfinity);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(overflow))
            .ConfigureIndicators(new Apo(1, 3, new Sma())).BuildAsync());
    }

    [Fact]
    public async Task SelectedInputReachesBothAliasesAndLegacyBuilderArms()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var apo = new Apo(3, 7);
        var price = new PriceOscillator(3, 7);
        apo.Of(source); price.Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, apo, price).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedApo(projected, 3, 7, 3);
        Assert.Equal(expected, run[apo].ToArray());
        Assert.Equal(expected, run[price].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var first = IndicatorCompute.ComputeApoFast(data, context, 3, 7);
            using var second = IndicatorCompute.ComputeAbsolutePriceOscillatorFast(data, context, 3, 7);
            Assert.Equal(expected, first.ToArray());
            Assert.Equal(expected, second.ToArray());
        }
    }
}
