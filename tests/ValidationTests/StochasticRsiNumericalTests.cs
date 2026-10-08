using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class StochasticRsiNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), BarsOf(prices).Select(b => b.Time));

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(-1074)]
    public async Task StartupAndIndependentLookbacksPreserveTheSmoothedRangePosition(int exponent)
    {
        var v = Math.ScaleB(1d, exponent); var prices = new[] { -v, v, -v, v, -v };
        var expected = new[] { 0d, 0, 0, 100d / 3, (2 * (100d / 3)) / 3 };
        var bars = BarsOf(prices); var indicator = new StochRsi(1, 2);
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, result[indicator.Outputs[0]].ToArray());
        Assert.Equal(expected, Data(prices).CalculateStochasticRelativeStrengthIndex(length: 1, stochLength: 2).CustomValuesList);
        var core = new double[prices.Length]; OscillatorCore.StochasticRsi(prices, core, 1, 2); Assert.Equal(expected, core);
        OscillatorCore.StochasticRsiOscillator(prices, core, 1, 2); Assert.Equal(expected, core);
        using var state = new StochasticRelativeStrengthIndexState(length: 1, stochLength: 2);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.Equal(expected[i], state.Update(Native(bars[i]), false, true).Value);
                Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
            }
        }
    }

    [Fact]
    public void RawBothOutputsUseSelectedPricesAndIndependentSmoothingPeriods()
    {
        var prices = new[] { 1d, 3, 1, 2, 4, 3 };
        var expected = BuiltInFormulaReferences.StochasticRsiOutputs(BarsOf(prices), new StochasticRelativeStrengthIndex(2, 1, 3));
        var data = Data(Enumerable.Repeat(9d, prices.Length).ToArray()); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeStochasticRsiFast(data, context, 2, 1, 3); Assert.Equal(expected["StochRsi"], raw.Span.ToArray());
        using var signal = IndicatorCompute.ComputeStochasticRsiSignalFast(data, context, 2, 1, 3); Assert.Equal(expected["Signal"], signal.Span.ToArray());
        OscillatorCore.StochasticRsi(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.StochasticRsi(prices, Array.Empty<double>()));
    }

    [Fact]
    public void NativeStochasticRsiRejectsInvalidFieldsWithoutAdvancing()
    {
        IStreamingIndicatorState Create() => new StochasticRelativeStrengthIndexState(length: 2);
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            var state = Create(); var control = Create();
            using var stateLifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var v in new[] { 3d, 1, 4 })
            {
                var seed = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, v, 1));
                state.Update(seed, true, true); control.Update(seed, true, true);
            }
            var fields = new[] { 2d, 4, 1, 2, 1 }; fields[field] = invalid;
            var bad = new OhlcvBar("GAINLOSS", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(new Bar(DateTime.UnixEpoch, 2, 4, 1, 2, 1));
            var actual = state.Update(good, true, true); var expected = control.Update(good, true, true);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Outputs!.OrderBy(p => p.Key), actual.Outputs!.OrderBy(p => p.Key));
        }
    }

    private static OhlcvBar Native(Bar b) => new("FOLDED", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public async Task FlatPricesCannotInventAStochasticRangeAfterUnderflow()
    {
        var prices = new[] { -1d, 1, 0 }.Concat(Enumerable.Repeat(0d, 1200)).ToArray();
        var indicator = new StochasticRelativeStrengthIndex(2, 1, 1, new Ema(2));
        using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(prices))).ConfigureIndicators(indicator).BuildAsync();
        Assert.All(result[indicator.Outputs[0]].ToArray().Skip(4), value => Assert.Equal(0d, value));
        using var state = new StochasticRelativeStrengthIndexState(MovingAvgType.ExponentialMovingAverage, 2, 1, 1);
        var live = BarsOf(prices).Select(bar => state.Update(Native(bar), true, true).Value).ToArray();
        Assert.All(live.Skip(4), value => Assert.Equal(0d, value));
    }

    [Fact]
    public void CoreAliasesPreserveBothLookbacksOnNonBinaryRsi()
    {
        var prices = new[] { 1d, 4, 2, 5, 3, 7, 4, 5, 1, 3, 8, 6 };
        var expected = BuiltInFormulaReferences.StochasticRsiOutputs(BarsOf(prices), new StochRsi(3, 7))["StochRsi"];
        var actual = new double[prices.Length];
        OscillatorCore.StochasticRsi(prices, actual, 3, 7); Assert.Equal(expected, actual);
        OscillatorCore.StochasticRsiOscillator(prices, actual, 3, 7); Assert.Equal(expected, actual);
    }

    private sealed class HalfAverage : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State();
        private sealed class State : IIndicatorState
        {
            public void Reset() { }
            public double Update(in Bar bar) => bar.Close / 2;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteCustomerStagesAreRejectedInsteadOfSilentlyPartiallyApplied(bool compact)
    {
        IIndicator indicator = compact ? new StochRsi(2, 3, new HalfAverage(), new HalfAverage())
            : new StochasticRelativeStrengthIndex(2, 1, 3, new HalfAverage(), new HalfAverage());
        await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(BarsOf(new[] { 1d, 3, 2, 4 }))).ConfigureIndicators(indicator).BuildAsync());
    }

    [Fact]
    public void RawCustomerStagesPreserveGainLossAndBothStochasticAverages()
    {
        foreach (var signal in new[] { false, true })
        {
            var averages = new[] { 2d, 6d, 21d, 42d }
                .Select(value => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)
                    ((values, _) => values.Select(_ => value).ToArray())).ToArray();
            using var scope = ComponentAverage.Arm(averages);
            using var context = new ComputeContext();
            var data = Data(new[] { 1d, 3, 2, 4 });
            using var result = signal ? IndicatorCompute.ComputeStochasticRsiSignalFast(data, context, 2, 1, 3)
                : IndicatorCompute.ComputeStochasticRsiFast(data, context, 2, 1, 3);
            Assert.Equal(Enumerable.Repeat(signal ? 42d : 21d, 4), result.Span.ToArray());
            Assert.Equal(signal ? 4 : 3, ComponentAverage.Substitutions);
        }
    }

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "StochRsi", "StochasticRelativeStrengthIndex", "StochasticRsiOscillator" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.StochasticRsiOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
