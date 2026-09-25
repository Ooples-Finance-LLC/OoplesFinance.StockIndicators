using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DirectionalStrengthNumericalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1019)]
    [InlineData(-1074)]
    public void WideCandleMovesKeepThePublishedRatioBounded(int exponent)
    {
        var highs = new[] { 20d, -20, 20 }.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var lows = new[] { 10d, -30, 10 }.Select(v => Math.ScaleB(v, exponent)).ToArray();
        var expected = new[] { 100d, -100, 100 };
        var times = Enumerable.Range(0, 3).Select(i => DateTime.UnixEpoch.AddMinutes(i)).ToArray();
        StockData Data(bool selected)
        {
            var data = new StockData(highs, highs, lows, highs, new[] { 1d, 1, 1 }, times);
            if (selected) data.CustomValuesList = new List<double> { 0, 0, 0 };
            return data;
        }
        Assert.Equal(expected, Data(false).CalculateDirectionalTrendIndex(length1: 1, length2: 1, length3: 1).CustomValuesList);
        foreach (var selected in new[] { false, true })
        {
            using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
            using var arm = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeDirectionalTrendIndexFast(Data(selected), context,
                length1: 1, length2: 1, length3: 1);
            Assert.Equal(selected ? new double[3] : expected, arm.Span.ToArray());
        }
        using var state = new DirectionalTrendIndexState(length1: 1, length2: 1, length3: 1);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < highs.Length; i++)
            {
                var input = new OhlcvBar("DIRECTION", BarTimeframe.Minutes(1), times[i], times[i], highs[i], highs[i], lows[i], highs[i], 1, true);
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, true, true).Value);
            }
        }
        var bars = highs.Select((v, i) => new Bar(times[i], v, v, lows[i], v, 1)).ToArray();
        var reference = BuiltInFormulaReferences.DirectionalStrengthOutputs(bars, new DirectionalTrendIndex(1))["Dti"];
        var core = new double[3];
        OoplesFinance.StockIndicators.Core.OscillatorCore.DirectionalTrendIndex(highs, lows, highs, core, 1);
        Assert.Equal(reference, core);
        OoplesFinance.StockIndicators.Core.OscillatorCore.DirectionalTrendIndex(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(), int.MaxValue);
    }

    [Fact]
    public void SymmetricExpansionCancelsFromZeroOrigin()
    {
        using var state = new DirectionalTrendIndexState(length1: 1, length2: 1, length3: 1);
        foreach (var value in new[] { double.MaxValue / 2, double.MaxValue })
        {
            var bar = new OhlcvBar("DIRECTION", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, 0, value, -value, 0, 1, true);
            Assert.Equal(0d, state.Update(bar, true, true).Value);
        }
    }

    [Fact]
    public async Task PublicSelectedClosePreservesOriginalHighLow()
    {
        var source = new CustomerScale(0);
        var original = new DirectionalTrendIndex(1);
        var indicator = original.Of(source);
        var bars = new[] { new Bar(DateTime.UnixEpoch, 20, 20, 10, 20, 1),
            new Bar(DateTime.UnixEpoch.AddMinutes(1), -20, -20, -30, -20, 1),
            new Bar(DateTime.UnixEpoch.AddMinutes(2), 20, 20, 10, 20, 1) };
        var expected = BuiltInFormulaReferences.DirectionalStrengthOutputs(bars, original)["Dti"];
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        Assert.Equal(expected, result[indicator].ToArray());
        var feed = Bars.Live();
        using var live = await new StockIndicatorBuilder().ConfigureSource(feed).PublishBeforeWarmup()
            .ConfigureIndicators(source, indicator).BuildAsync();
        foreach (var bar in bars) feed.Publish(bar);
        feed.Complete();
        var actual = new List<double>();
        await foreach (var snapshot in live) actual.Add(snapshot[indicator.Outputs[0]]);
        Assert.Equal(expected, actual);
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
    public async Task PublicDirectionalRatioUsesAllSixCustomerStages()
    {
        var indicator = new DirectionalTrendIndex(3, new CustomerScale(1), new CustomerScale(1), new CustomerScale(1),
            new CustomerScale(1), new CustomerScale(1), new CustomerScale(2));
        var bars = new[] { new Bar(DateTime.UnixEpoch, 1, 2, 1, 1, 1),
            new Bar(DateTime.UnixEpoch.AddMinutes(1), 1, 3, 1, 1, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 1, 3, 0, 1, 1) };
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(new[] { 50d, 50, -50 }, result[indicator].ToArray());
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal)
    {
        "DirectionalTrendIndex"
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
