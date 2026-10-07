using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SmoothedDeltaNumericalTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1019, 1)]
    [InlineData(-1074, 1)]
    [InlineData(0, 2)]
    [InlineData(1019, 2)]
    [InlineData(-1074, 2)]
    public void LaggedMeanMovementMatchesHandCalculatedRatios(int exponent, int length)
    {
        var rawPrices = length == 1 ? new[] { -20d, 20, 0, -20, 20 } : new[] { 0d, 10, 20, 0, 5, 15 };
        var expected = length == 1 ? new[] { 0d, 1, 0, 0, 1 } : new[] { 0d, 0, 1, 1d / 3, 0, 0 };
        var prices = rawPrices.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = (IBuiltInIndicator)new SmoothedDeltaRatioOscillator(length);
        Assert.Equal(expected, BuiltInFormulaReferences.SmoothedDeltaOutputs(bars, indicator)["Sdro"]);
        StockData Data(bool selected)
        {
            var raw = selected ? prices.Select(_ => 100d).ToArray() : prices;
            var data = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), bars.Select(b => b.Time));
            if (selected) data.CustomValuesList = prices.ToList();
            return data;
        }
        Assert.Equal(expected, Data(false).CalculateSmoothedDeltaRatioOscillator(length: length).CustomValuesList);
        var core = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.SmoothedDeltaRatioOscillator(prices, core, length);
        Assert.Equal(expected, core);
        OoplesFinance.StockIndicators.Core.OscillatorCore.SmoothedDeltaRatioOscillator(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
        foreach (var selected in new[] { false, true })
        {
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeSmoothedDeltaRatioOscillatorFast(Data(selected), context, length);
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
                    var input = new OhlcvBar("DELTA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
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
    public async Task PublicRatioHonorsBothCustomerAverages()
    {
        var indicator = new SmoothedDeltaRatioOscillator(1, new CustomerScale(.25), new CustomerScale(.5));
        var bars = new[] { 0d, 20, 10, 30 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 0d, .5, 0, .5 }, result[indicator].ToArray());
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "SmoothedDeltaRatioOscillator"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.SmoothedDeltaOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
