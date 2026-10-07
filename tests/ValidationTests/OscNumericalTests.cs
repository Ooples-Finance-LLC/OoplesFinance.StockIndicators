using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class OscNumericalTests
{
    public static IEnumerable<object[]> HandExamples => new[]
    {
        new object[] { new[] { 2d, 4, 8, 16, 32 }, new[] { 0d, -3, -6, -4.5, -9 } },
        new object[] { Enumerable.Repeat(double.MaxValue, 5).ToArray(), new[] { 0d, -double.MaxValue, -double.MaxValue, 0, 0 } },
        new object[] { Enumerable.Repeat(double.Epsilon, 5).ToArray(), new[] { 0d, -double.Epsilon, -double.Epsilon, 0, 0 } }
    };

    [Theory, MemberData(nameof(HandExamples))]
    public void PublishedDifferencePreservesStartupSignAndExtremeMeans(double[] prices, double[] expected)
    {
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = (IBuiltInIndicator)new OscOscillator(4);
        StockData Data(bool selected)
        {
            var raw = selected ? prices.Select(_ => 100d).ToArray() : prices;
            var data = new StockData(raw, raw, raw, raw, prices.Select(_ => 1d), bars.Select(b => b.Time));
            if (selected) data.CustomValuesList = prices.ToList();
            return data;
        }
        Assert.Equal(expected, Data(false).CalculateOscOscillator(fastLength: 2, slowLength: 4).CustomValuesList);
        var output = new double[prices.Length];
        OoplesFinance.StockIndicators.Core.OscillatorCore.OscOscillator(prices, output, 2, 4);
        Assert.Equal(expected, output);
        OoplesFinance.StockIndicators.Core.OscillatorCore.OscOscillator(Array.Empty<double>(), Array.Empty<double>(), int.MaxValue, int.MaxValue);
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
                    var input = new OhlcvBar("OSC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
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
    public async Task PublicDifferenceHonorsBothCustomerMeans()
    {
        var indicator = new OscOscillator(4, new CustomerScale(1), new CustomerScale(.5));
        var bars = new[] { 2d, 4, 8 }.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { -1d, -2, -4 }, result[indicator].ToArray());
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "OscOscillator"
    };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route);

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
