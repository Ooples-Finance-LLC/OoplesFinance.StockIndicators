using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedHurstRankTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersHurstCoefficient) || c.IndicatorType == typeof(EhlersNoiseEliminationTechnology) || c.IndicatorType == typeof(EhlersSpearmanRankIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new EhlersHurstCoefficient(length, 3), 1 => new EhlersNoiseEliminationTechnology(length),
        _ => new EhlersSpearmanRankIndicator(length) };
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2 }
        from length in new[] { int.MinValue, 0, 1, 2, 7, 20, 64 } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task PaddingTiesAndWideRangesRetainBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Range(0, 47).Select(i => (i * 13 % 101 - 50) / 8d).ToArray(),
            Enumerable.Repeat(1d, 25).Concat(Enumerable.Repeat(0d, 25)).ToArray(),
            [3, 2, 3, 1, 2, 2, 1, 3, 0, 0, 0, -1, -1] }) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(1, int.MaxValue)] [InlineData(int.MaxValue, 3)]
    [InlineData(7, 2)] [InlineData(2, 7)] [InlineData(30, 20)] [InlineData(0, int.MinValue)]
    public async Task HurstIndependentRangeAndSmoothingRetainBits(int length, int smooth) =>
        await Compare([new EhlersHurstCoefficient(length, smooth)], ToBars([1, 3, 2, 0, 0, -2, -3, 5, 1, 0, 2, 3, -1]));

    [Fact]
    public void NoiseScratchOverflowRetainsFallback() =>
        Assert.False(ValuesBarExecution.SupportsOwned([new EhlersNoiseEliminationTechnology(int.MaxValue)]));

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var selected in new IIndicator[] { new EhlersHurstCoefficient(7, 3).Of(new Sma(2)),
            new EhlersNoiseEliminationTechnology(7).Of(new Sma(2)), new EhlersSpearmanRankIndicator(7).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(1, 7);
        IIndicator[] roots = [repeated, Create(0, 7), Create(2, 7), new EhlersCorrelationAngleIndicator(7), repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var config in new[] { (Name: "GeneratedHurstCoefficientState", Arguments: new object[] { 7, 3 }),
            (Name: "GeneratedNoiseEliminationState", Arguments: new object[] { 7 }),
            (Name: "GeneratedChronologicalSpearmanState", Arguments: new object[] { 7 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IIndicatorState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            try { var expected = bars.Select(bar => state.Update(in bar)).ToArray(); state.Reset(); Bits(expected, bars.Select(bar => state.Update(in bar)).ToArray()); }
            finally { (state as IDisposable)?.Dispose(); }
        }
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

