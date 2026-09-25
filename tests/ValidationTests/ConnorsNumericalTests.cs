using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ConnorsNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), BarsOf(prices).Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("CONNORS", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Fact]
    public void AllDirectRoutesPreserveExactRanksSelectedInputsPeriodsPreviewAndReset()
    {
        var prices = new[] { double.Epsilon, double.MaxValue / 2, double.Epsilon, double.MaxValue, -double.MaxValue, 0d, 1, 4, 2, 7, 3, 8, 1, 1, 2 };
        var bars = BarsOf(prices); var indicator = new ConnorsRelativeStrengthIndex(2, 3, 4);
        var expected = BuiltInFormulaReferences.ConnorsOutputs(bars, indicator);
        Assert.Equal(25, expected["PctRank"][1]); Assert.Equal(75, expected["PctRank"][3]);
        var batch = Data(prices).CalculateConnorsRelativeStrengthIndex(length1: 2, length2: 3, length3: 4);
        foreach (var pair in expected) Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
        var data = Data(Enumerable.Repeat(9d, prices.Length).ToArray()); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        foreach (var (key, series) in new[] { ("Rsi", IndicatorCompute.ConnorsRsiSeries.PriceStrength), ("PctRank", IndicatorCompute.ConnorsRsiSeries.PercentRank),
            ("StreakRsi", IndicatorCompute.ConnorsRsiSeries.StreakStrength), ("ConnorsRsi", IndicatorCompute.ConnorsRsiSeries.ConnorsRsi) })
        {
            using var raw = IndicatorCompute.ComputeConnorsRsiFast(data, context, 2, 3, 4, series: series);
            Assert.Equal(expected[key], raw.Span.ToArray());
        }
        var core = new double[prices.Length]; OscillatorCore.ConnorsRsi(prices, core, 3, 2, 4); Assert.Equal(expected["ConnorsRsi"], core);
        OscillatorCore.ConnorsRelativeStrengthIndex(prices, core, 3, 2, 4); Assert.Equal(expected["ConnorsRsi"], core);
        OscillatorCore.ConnorsRsi(Array.Empty<double>(), Array.Empty<double>());
        Assert.Throws<ArgumentException>(() => OscillatorCore.ConnorsRsi(prices, Array.Empty<double>()));
        using var state = new ConnorsRelativeStrengthIndexState(length1: 2, length2: 3, length3: 4);
        Replay(state, bars, expected);

        var stochastic = new StochasticConnorsRelativeStrengthIndex(2, 3, 4, 2, 5);
        var stochExpected = BuiltInFormulaReferences.ConnorsOutputs(bars, stochastic);
        var stochBatch = Data(prices).CalculateStochasticConnorsRelativeStrengthIndex(length1: 2, length2: 3, length3: 4, smoothLength1: 2, smoothLength2: 5);
        foreach (var pair in stochExpected)
        {
            Assert.Equal(pair.Value, stochBatch.OutputValues[pair.Key]);
            using var raw = IndicatorCompute.ComputeStochasticConnorsRsiFast(data, context, 2, 3, 4, 2, 5, outputKey: pair.Key);
            Assert.Equal(pair.Value, raw.Span.ToArray());
        }
        using var stochState = new StochasticConnorsRelativeStrengthIndexState(length1: 2, length2: 3, length3: 4, smoothLength1: 2, smoothLength2: 5);
        Replay(stochState, bars, stochExpected);
    }

    [Fact]
    public void TypedAndLiveFactoriesPreserveEveryPeriod()
    {
        var bars = BarsOf(new[] { 1d, 4, 2, 5, 3, 8, 6, 4, 7, 2, 1, 3 });
        foreach (var indicator in new IBuiltInIndicator[] { new ConnorsRsi(4), new ConnorsRelativeStrengthIndex(2, 5, 7),
            new StochasticConnorsRelativeStrengthIndex(2, 5, 7, 3, 4) })
        {
            var expected = BuiltInFormulaReferences.ConnorsOutputs(bars, indicator);
            var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(indicator.BatchName, indicator.CreateOptions());
            var native = StatefulIndicatorFactory.Create(spec); var live = StreamingIndicatorFactory.CreateState(spec);
            Assert.NotNull(live);
            using var nativeLifetime = native as IDisposable; using var liveLifetime = live as IDisposable;
            Replay(native, bars, expected); Replay(live!, bars, expected);
        }
    }

    private static void Replay(IStreamingIndicatorState state, Bar[] bars, IReadOnlyDictionary<string, double[]> expected)
    {
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            foreach (var final in new[] { false, true })
            {
                var actual = state.Update(Native(bars[i]), final, true);
                foreach (var pair in expected) Assert.Equal(pair.Value[i], actual.Outputs![pair.Key]);
            }
        }
    }

    [Fact]
    public void RawCustomerStagesPreserveAllFourRsiAveragesAndBothStochasticStages()
    {
        var prices = new[] { 1d, 3, 2, 4 }; var data = Data(prices);
        using var context = new ComputeContext();
        foreach (var (series, expected) in new[] { (IndicatorCompute.ConnorsRsiSeries.PriceStrength, 25d), (IndicatorCompute.ConnorsRsiSeries.StreakStrength, 75d) })
        {
            var means = new[] { 2d, 6d, 6d, 2d }.Select(value => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)
                ((values, _) => values.Select(_ => value).ToArray())).ToArray();
            using var scope = ComponentAverage.Arm(means);
            using var output = IndicatorCompute.ComputeConnorsRsiFast(data, context, 2, 3, 4, series: series);
            Assert.Equal(Enumerable.Repeat(expected, prices.Length), output.Span.ToArray());
            Assert.Equal(4, ComponentAverage.Substitutions);
        }
        foreach (var signal in new[] { false, true })
        {
            var averages = new[] { 2d, 6d, 6d, 2d, 21d, 42d }
                .Select(value => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)((values, _) => values.Select(_ => value).ToArray())).ToArray();
            using var scope = ComponentAverage.Arm(averages);
            using var output = IndicatorCompute.ComputeStochasticConnorsRsiFast(data, context, 2, 3, 4, 2, 5, outputKey: signal ? "Signal" : "SaRsi");
            Assert.Equal(Enumerable.Repeat(signal ? 42d : 21d, prices.Length), output.Span.ToArray());
            Assert.Equal(signal ? 6 : 5, ComponentAverage.Substitutions);
        }
    }

    private sealed class ConstantAverage : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State();
        private sealed class State : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => 2; }
    }

    [Fact]
    public async Task PublicCustomerSubstitutionsRejectIncompleteStageLists()
    {
        foreach (var indicator in new IIndicator[] { new ConnorsRelativeStrengthIndex(2, 3, 4, new ConstantAverage()),
            new StochasticConnorsRelativeStrengthIndex(2, 3, 4, 2, 5, new ConstantAverage(), new ConstantAverage()) })
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(new[] { 1d, 3, 2, 4 }))).ConfigureIndicators(indicator).BuildAsync();
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeConnorsRejectsInvalidFieldsWithoutAdvancing(bool stochastic)
    {
        IStreamingIndicatorState Create() => stochastic ? new StochasticConnorsRelativeStrengthIndexState(length1: 2, length2: 3, length3: 4) : new ConnorsRelativeStrengthIndexState(length1: 2, length2: 3, length3: 4);
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

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "ConnorsRsi", "ConnorsRelativeStrengthIndex", "StochasticConnorsRelativeStrengthIndex" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.ConnorsOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
