using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WamiNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1019)]
    [InlineData(-1074)]
    public void WideDifferencesCancelBeforePublicationAcrossAllRoutes(int exponent)
    {
        var prices = new[] { 20d, -20, 0, 20, 20 }.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var expected = new[] { 0d, -16, -4, 6, 6 }.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = (IBuiltInIndicator)new WamiOscillator(1);
        Assert.Equal(expected, BuiltInFormulaReferences.WamiOutputs(bars, indicator)["Wami"]);
        StockData Data(bool selected)
        {
            var raw = selected ? prices.Select(_ => 100d).ToArray() : prices;
            var data = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), bars.Select(b => b.Time));
            if (selected) data.CustomValuesList = prices.ToList();
            return data;
        }
        Assert.Equal(expected, Data(false).CalculateWamiOscillator(length1: 1).CustomValuesList);
        var core = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.WamiOscillator(prices, prices, prices, core, 1);
        Assert.Equal(expected, core);
        OoplesFinance.StockIndicators.Core.OscillatorCore.WamiOscillator(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
        foreach (var selected in new[] { false, true })
        {
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeArm(Data(selected), spec, context);
            Assert.NotNull(arm);
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
                    var input = new OhlcvBar("WAMI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    Assert.Equal(expected[i], state.Update(input, false, true).Value);
                    Assert.Equal(expected[i], state.Update(input, true, true).Value);
                }
            }
        }
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
    public async Task PublicComponentsLeaveTheFixedWeightedStageIntact()
    {
        var indicator = new WamiOscillator(3, new CustomerScale(1), new CustomerScale(.5));
        var bars = new[] { 0d, 10, 10 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, 2, 1.5 }, result[indicator].ToArray());
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "WamiOscillator"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.WamiOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
