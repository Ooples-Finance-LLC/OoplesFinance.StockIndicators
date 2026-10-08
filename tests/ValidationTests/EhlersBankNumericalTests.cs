using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class EhlersBankNumericalTests
{
    [Theory]
    [InlineData(1e-200)]
    [InlineData(1d)]
    [InlineData(1e200)]
    [InlineData(double.MaxValue)]
    public void SpectrumPreservesCycleAcrossPriceScales(double scale)
    {
        var prices = Enumerable.Range(0, 1200).Select(i => .5 + .2 * Math.Sin(2 * Math.PI * i / 17)).ToArray();
        var scaled = prices.Select(v => v * scale).ToArray();
        var expected = new double[prices.Length];
        var actual = new double[prices.Length];
        Core.OscillatorCore.EhlersSpectrumDerivedFilterBank(prices, expected);
        Core.OscillatorCore.EhlersSpectrumDerivedFilterBank(scaled, actual);
        var bars = scaled.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var reference = BuiltInFormulaReferences.SpectrumCycles(bars, 8, 50, 40, 10);
        for (var i = 0; i < prices.Length; i++)
        {
            Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, 1e-8);
            Assert.InRange(Math.Abs(reference[i] - expected[i]), 0, 1e-8);
        }
        Assert.All(actual.Skip(1000), cycle => Assert.InRange(cycle, 16, 18));
    }

    [Fact]
    public void ScaleChangesAndExtremePreviewsPreserveTheCommittedSpectrum()
    {
        var prices = Enumerable.Range(0, 128).Select(i => i < 4 ? 0 :
            (i < 40 ? 1e-200 : i < 80 ? 1d : 1e300) * Math.Sin(i / 3d)).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.SpectrumCycles(bars, 8, 50, 40, 10);
        using var state = new EhlersSpectrumDerivedFilterBankEngine(8, 50, 40, 10);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                state.Next(-double.MaxValue, false);
                var preview = state.Next(prices[i], false);
                var actual = state.Next(prices[i], true);
                Assert.Equal(preview, actual);
                Assert.InRange(Math.Abs(actual - expected[i]), 0, 1e-8);
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void GaussianFactoriesHonorThePrimaryPole(int poles)
    {
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(
            OoplesFinance.StockIndicators.Enums.IndicatorName.EhlersGaussianFilter,
            new OoplesFinance.StockIndicators.Builder.Specs.EhlersGaussianFilterSpecOptions(14, poles));
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            var result = state.Update(new OhlcvBar("POLES", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch,
                1, 1, 1, 1, 1, true), true, true);
            Assert.Equal(result.Outputs!["Egf" + poles], result.Value);
            Assert.NotEqual(result.Outputs["Egf1"], result.Outputs["Egf4"]);
        }
    }

    [Fact]
    public void DemodulatorFactoryPreservesUnsaturatedGain()
    {
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(
            OoplesFinance.StockIndicators.Enums.IndicatorName.EhlersFMDemodulatorIndicator,
            new OoplesFinance.StockIndicators.Builder.Specs.EhlersFMDemodulatorIndicatorSpecOptions(2, 1, MovingAvgType.WeightedMovingAverage));
        foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
        {
            Assert.NotNull(state);
            using var lifetime = state as IDisposable;
            Assert.Equal(.25, state.Update(new OhlcvBar("GAIN", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch,
                0, 1, 0, .125, 1, true), true, true).Value);
        }
    }

    [Fact]
    public void PullOverflowPreviewDoesNotInvalidateCommittedSignal()
    {
        using var state = new EhlersRestoringPullIndicatorState(minLength: 3, maxLength: 3);
        OhlcvBar Input(double volume) => new("PULL", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch,
            1, 1, 1, 1, volume, true);
        Assert.True(double.IsPositiveInfinity(state.Update(Input(double.MaxValue), false, true).Value));
        Assert.True(double.IsFinite(state.Update(Input(1), true, true).Outputs!["Signal"]));
        Assert.True(double.IsPositiveInfinity(state.Update(Input(double.MaxValue), true, true).Value));
        Assert.True(double.IsNaN(state.Update(Input(1), true, true).Outputs!["Signal"]));
        state.Reset();
        Assert.True(double.IsFinite(state.Update(Input(1), true, true).Outputs!["Signal"]));
    }

    [Fact]
    public void SpectrumSignalsCompareSuccessiveValuesInTheSameScale()
    {
        var prices = new[] { 1d, 10 };
        var result = new StockData(prices, prices, prices, prices, new[] { 1d, 1 },
            new[] { DateTime.UnixEpoch, DateTime.UnixEpoch.AddMinutes(1) }).CalculateEhlersSpectrumDerivedFilterBank();
        Assert.Equal(new[] { Signal.StrongBuy, Signal.StrongBuy }, result.SignalsList);
    }

    [Fact]
    public void RestoringPullFastRouteUsesTheSelectedPrice()
    {
        var selected = Enumerable.Range(0, 128).Select(i => Math.Sin(2 * Math.PI * i / 17)).ToArray();
        var original = selected.Select(_ => 100d).ToArray();
        var times = selected.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)).ToArray();
        var data = new StockData(original, original, original, original, selected.Select(_ => 1d), times);
        data.CustomValuesList = selected.ToList();
        var projected = selected.Select((v, i) => new Bar(times[i], 100, 100, 100, v, 1)).ToArray();
        var indicator = new EhlersRestoringPullIndicator();
        var expected = BuiltInFormulaReferences.RestoringPullOutputs(projected, ((IBuiltInIndicator)indicator).CreateOptions());
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        foreach (var signal in new[] { false, true })
        {
            using var result = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeEhlersRestoringPullIndicatorFast(data, context, signal: signal);
            var actual = result.ToArray();
            var values = expected[signal ? "Signal" : "Rpi"];
            for (var i = 0; i < actual.Length; i++) Assert.InRange(Math.Abs(actual[i] - values[i]), 0, 1e-9);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "EhlersSpectrumDerivedFilterBank", "EhlersRestoringPullIndicator", "EhlersGaussianFilter", "EhlersFMDemodulatorIndicator"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, testCase.IndicatorType == typeof(EhlersRestoringPullIndicator)
            ? bars => BuiltInFormulaReferences.RestoringPullOutputs(bars, ((IBuiltInIndicator)testCase.Factory()).CreateOptions()) : null);

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
