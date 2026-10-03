using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MomentaNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1019)]
    [InlineData(-1074)]
    public void WideRangesPublishTheHandCalculatedPercentage(int exponent)
    {
        var prices = new[] { -20d, 20, 0, -20, 20 }.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var expected = new[] { 0d, 100, 50, 0, 100 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = (IBuiltInIndicator)new DoubleSmoothedMomenta(3, 1, 1);
        var reference = BuiltInFormulaReferences.MomentaOutputs(bars, indicator);
        Assert.Equal(expected, reference["Dsm"]);
        Assert.Equal(expected, reference["Signal"]);
        StockData Data(bool selected)
        {
            var raw = selected ? prices.Select(_ => 100d).ToArray() : prices;
            var data = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), bars.Select(b => b.Time));
            if (selected) data.CustomValuesList = prices.ToList();
            return data;
        }
        Assert.Equal(expected, Data(false).CalculateDoubleSmoothedMomenta(length1: 3, length2: 1, length3: 1).CustomValuesList);
        var core = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.DoubleSmoothedMomenta(prices, core, 3, 1, 1);
        Assert.Equal(expected, core);
        OoplesFinance.StockIndicators.Core.OscillatorCore.DoubleSmoothedMomenta(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
        foreach (var selected in new[] { false, true })
        foreach (var key in new[] { "Dsm", "Signal" })
        {
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeDoubleSmoothedMomentaFast(
                Data(selected), context, 3, length2: 1, length3: 1, outputKey: key);
            Assert.Equal(expected, arm.Span.ToArray());
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
                    var input = new OhlcvBar("MOMENTA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(input, false, true).Value);
                    Assert.Equal(expected[i], state.Update(input, false, true).Outputs!["Signal"]);
                    Assert.Equal(expected[i], state.Update(input, true, true).Value);
                }
            }
            // Restart at a different first value: replaying the same minimum can hide stale extrema.
            state.Reset();
            var last = bars[^1];
            var restart = new OhlcvBar("MOMENTA", BarTimeframe.Minutes(1), last.Time, last.Time,
                last.Open, last.High, last.Low, last.Close, last.Volume, true);
            Assert.Equal(0d, state.Update(restart, false, true).Value);
            Assert.Equal(0d, state.Update(restart, true, true).Value);
        }
    }

    [Fact]
    public void MinimumRangeStillUsesTwoBars()
    {
        var prices = new[] { 0d, 10, 5 };
        var expected = new[] { 0d, 100, 0 };
        var core = new double[3];
        OoplesFinance.StockIndicators.Core.OscillatorCore.DoubleSmoothedMomenta(prices, core, 1, 1, 1);
        Assert.Equal(expected, core);
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
        Assert.Equal(expected, data.CalculateDoubleSmoothedMomenta(length1: 1, length2: 1, length3: 1).CustomValuesList);
    }

    private sealed class CustomerScale(double factor) : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State(factor);
        private sealed class State(double factor) : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close * factor;
        }
    }

    [Fact]
    public async Task PublicRatioAndSignalHonorAllFiveCustomerStages()
    {
        var indicator = new DoubleSmoothedMomenta(3, 1, 1, new CustomerScale(.5), new CustomerScale(.5),
            new CustomerScale(1), new CustomerScale(1), new CustomerScale(.5));
        var bars = new[] { 0d, 20, 10 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, 25, 12.5 }, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(new[] { 0d, 12.5, 6.25 }, result[indicator.Outputs[1]].ToArray());
        var feed = Bars.Live();
        var error = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup()
                .ConfigureIndicators(indicator).BuildAsync();
        });
        Assert.Contains("separately configured average stages", error.Message);
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "DoubleSmoothedMomenta"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.MomentaOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
