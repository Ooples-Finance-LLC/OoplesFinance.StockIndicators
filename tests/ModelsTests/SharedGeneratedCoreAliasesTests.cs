using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedCoreAliasesTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(AverageDirectionalIndex) || c.IndicatorType == typeof(MomentumOscillator)
            || c.IndicatorType == typeof(VolumePriceTrend) || c.IndicatorType == typeof(StandardDevation)
            || c.IndicatorType == typeof(Vhf)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new AverageDirectionalIndex(length), 1 => new MomentumOscillator(length, 3),
        2 => new VolumePriceTrend(length), 3 => new StandardDevation(length, MovingAvgType.SimpleMovingAverage), _ => new Vhf(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 5)
        from length in new[] { int.MinValue, 1, 2, 20, 65, 1024 } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task AliasPeriodsRetainAllOutputBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task StandardDeviationBatchSignalRetainsFlatPrefixRounding()
    {
        var bars = ToBars(Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray());
        var alias = new StandardDevation(20, MovingAvgType.SimpleMovingAverage);
        await Compare([alias, new StdDev(20)], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(alias).BuildAsync();
        var signal = run[alias.Outputs[1]].ToArray();
        Assert.Equal(4577435457191573576L, BitConverter.DoubleToInt64Bits(signal[31]));
        Assert.Equal(4583121594755959680L, BitConverter.DoubleToInt64Bits(signal[32]));
    }

    [Fact]
    public async Task MomentumObsoleteSmoothLengthRemainsIgnored()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var shortRoot = new MomentumOscillator(5, 1); var longRoot = new MomentumOscillator(5, int.MaxValue);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(shortRoot, longRoot).BuildAsync();
        for (var i = 0; i < shortRoot.Outputs.Count; i++) Bits(run[shortRoot.Outputs[i]].ToArray(), run[longRoot.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task ExplicitAverageSelectionsRetainFallback()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new AverageDirectionalIndex(14, new Sma(3)),
            new MomentumOscillator(10, 3, new Sma(3)), new VolumePriceTrend(14, new Sma(3)), new StandardDevation(14, MovingAvgType.ExponentialMovingAverage) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public async Task ExplicitSourcesRetainFallback()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new AverageDirectionalIndex(14).Of(new Sma(2)),
            new MomentumOscillator(10, 3).Of(new Sma(2)), new VolumePriceTrend(14).Of(new Sma(2)),
            new StandardDevation(14, MovingAvgType.SimpleMovingAverage).Of(new Sma(2)), new Vhf(14).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }

    [Fact]
    public async Task RepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var repeated = Create(4, 20);
        IIndicator[] roots = [repeated, Create(0, 10), Create(1, 9), Create(2, 20), Create(3, 3), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task OriginalHighLowAndVolumeRetainBits()
    {
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 100 + i,
            15 + i % 7, 1 + i % 3, 4 + i % 5, i % 9 == 0 ? 0 : 100 + i * 3)).ToArray();
        await Compare(Enumerable.Range(0, 5).Select(i => Create(i, 14)).ToArray(), bars);
    }

    [Fact]
    public async Task HugeOwnedPeriodsAvoidLegacyPeriodSizedRentals()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        for (var kind = 0; kind < 5; kind++)
        {
            var root = Create(kind, int.MaxValue); Assert.True(ValuesBarExecution.SupportsOwned([root]));
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
            Assert.False(((IndicatorRun)run).HasLegacyRuntime);
            foreach (var output in root.Outputs)
            {
                var values = run[output].ToArray(); Assert.Equal(bars.Length, values.Length);
                Assert.All(values, value => Assert.True(double.IsFinite(value)));
                if (kind is 1 or 3) Bits(new double[bars.Length], values);
            }
        }
    }

    [Fact]
    public void OutputShapesAndDirectionalPrimaryRemainUnchanged()
    {
        var direction = new AverageDirectionalIndex(14);
        Assert.Equal(3, direction.Outputs.Count); Assert.Same(direction.Outputs[2], direction.Value);
        for (var i = 1; i < 5; i++) Assert.Equal(2, Create(i, 14).Outputs.Count);
    }

    private static Bar[] ToBars(double[] prices) => prices.Select((v, i) => new Bar(default,
        i % 5 == 0 ? 0 : v / 4, Math.Abs(v), i % 7 == 0 ? 0 : Math.Abs(v) / 2, v, 1)).ToArray();

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
            IIndicatorRun? expected = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    IIndicatorRun? actual = null;
                    try
                    {
                        var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                        Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                        Assert.Equal(expectedError?.Message, actualError?.Message);
                        if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                        Assert.NotNull(expected); Assert.NotNull(actual);
                        if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned(roots))
                            Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
                        if (bars.Length > 0) { Assert.Equal(expected.Latest.Bar, actual.Latest.Bar); Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp); }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
                    }
                    finally { actual?.Dispose(); }
                }
            }
            finally { expected?.Dispose(); }
        }
    }

    private static void Bits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
