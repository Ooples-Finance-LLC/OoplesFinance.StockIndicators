using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class QuasiWhiteNoiseNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    private static StockData Data(double[] prices) => new(prices, prices, prices, prices, Enumerable.Repeat(1d, prices.Length), BarsOf(prices).Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("NOISE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory]
    [InlineData(8d)]
    [InlineData(-8d)]
    [InlineData(1.7976931348623157E+308)]
    public void AllOutputsPreserveScalingSelectedInputsPopulationWindowAndNativePeriods(double divisor)
    {
        var prices = new[] { double.Epsilon, double.MaxValue / 2, double.Epsilon, double.MaxValue, -double.MaxValue, 0d, 1, 4, 2, 7, 3, 8, 1, 1, 2 };
        var bars = BarsOf(prices); var indicator = new QuasiWhiteNoise(3, 5, divisor);
        var expected = BuiltInFormulaReferences.QuasiWhiteNoiseOutputs(bars, indicator);
        Assert.Contains(expected["WhiteNoiseStdDev"], v => v > 0);
        var batch = Data(prices).CalculateQuasiWhiteNoise(length: 3, noiseLength: 5, divisor: divisor);
        foreach (var pair in expected) Assert.Equal(pair.Value, batch.OutputValues[pair.Key]);
        var data = Data(Enumerable.Repeat(9d, prices.Length).ToArray()); data.CustomValuesList = prices.ToList();
        using var context = new ComputeContext();
        foreach (var series in Enum.GetValues<IndicatorCompute.QuasiWhiteNoiseSeries>())
        {
            using var raw = IndicatorCompute.ComputeQuasiWhiteNoiseFast(data, context, 3, 5, divisor, series: series);
            Assert.Equal(expected[series.ToString()], raw.Span.ToArray());
        }
        using var direct = new QuasiWhiteNoiseState(length: 3, noiseLength: 5, divisor: divisor);
        Replay(direct, bars, expected);
        var builtIn = (IBuiltInIndicator)indicator;
        var spec = new OoplesFinance.StockIndicators.Builder.Specs.IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions());
        var native = StatefulIndicatorFactory.Create(spec); var live = StreamingIndicatorFactory.CreateState(spec);
        Assert.NotNull(live);
        using var nativeLifetime = native as IDisposable; using var liveLifetime = live as IDisposable;
        Replay(native, bars, expected); Replay(live!, bars, expected);
    }

    [Fact]
    public void RawCustomerStagesPreserveAllRsiAveragesAndThePublishedMean()
    {
        var means = new[] { 2d, 6d, 6d, 2d, 21d }.Select(value => (Func<IReadOnlyList<double>, int, IReadOnlyList<double>>)
            ((values, _) => values.Select(_ => value).ToArray())).ToArray();
        using var scope = ComponentAverage.Arm(means); using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeQuasiWhiteNoiseFast(Data(new[] { 1d, 3, 2, 4 }), context, 3, 5, 8,
            series: IndicatorCompute.QuasiWhiteNoiseSeries.WhiteNoiseMa);
        Assert.Equal(Enumerable.Repeat(21d, 4), result.Span.ToArray()); Assert.Equal(5, ComponentAverage.Substitutions);
    }

    private sealed class ConstantAverage : IndicatorBase, IMovingAverage
    {
        protected internal override object? CreateState() => new State();
        private sealed class State : IIndicatorState { public void Reset() { } public double Update(in Bar bar) => 2; }
    }

    [Fact]
    public async Task PublicCustomerAverageRejectsIncompleteStageList()
    {
        var indicator = new QuasiWhiteNoise(3, 5, 8, new ConstantAverage());
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            using var result = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf(new[] { 1d, 3, 2, 4 }))).ConfigureIndicators(indicator).BuildAsync();
        });
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
    public void NativeNoiseRejectsInvalidFieldsWithoutAdvancing()
    {
        IStreamingIndicatorState Create() => new QuasiWhiteNoiseState(length: 3, noiseLength: 5, divisor: 8);
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

    internal static readonly HashSet<string> Families = new(StringComparer.Ordinal) { "QuasiWhiteNoise" };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => Families.Contains(c.IndicatorType.Name)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));

    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.QuasiWhiteNoiseOutputs(bars, (IBuiltInIndicator)testCase.Factory()));

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
