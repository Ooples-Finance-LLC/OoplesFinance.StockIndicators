using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedPrimeFamilyTests
{
    public static IEnumerable<object[]> Cases =>
        from bands in new[] { false, true }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 4096, int.MaxValue }
        select new object[] { bands, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactPrimeOffsetsAndRetainedExtremaMatchLegacy(bool bands, int length)
    {
        IIndicator root = bands ? new PrimeNumberBands(length) : new PrimeNumberOscillator(length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-8, 0, 1.5, 2.5, 4, 11, 12, 14, 17, 18, 20, 24, 8, 100],
            [-9223372036854775808d, 9223372036854774784d, 2, 3, 4],
            Enumerable.Range(0, 65).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        })
            await Compare([root], prices.Select(v => new Bar(default, v / 4, v, v / 2, v, 1)).ToArray());
    }

    [Fact]
    public async Task DomainRootOrderAndInvalidTailMatchLegacy()
    {
        foreach (var prime in new IIndicator[] { new PrimeNumberOscillator(3), new PrimeNumberBands(3) })
        foreach (var reverse in new[] { false, true })
        {
            IIndicator[] roots = reverse ? [new AbsoluteStrengthIndex(3), prime] : [prime, new AbsoluteStrengthIndex(3)];
            var bad = new Bar(default, double.MaxValue, 1, -1, -1, 1);
            await Compare(roots, [bad]);
            await Compare(roots, [bad, new(default, 1, 1, 1, 1, double.NaN)]);
            await Compare(roots, [new(default, 1, 1, 1, -1, 1), new(default, double.MaxValue, 1, 1, 1, 1)]);
            foreach (var field in Enumerable.Range(0, 4))
            {
                var values = new[] { 1d, 1, 1, 1 }; values[field] = double.MaxValue;
                await Compare([prime], [new(default, values[0], values[1], values[2], values[3], 1)]);
            }
        }
    }

    [Fact]
    public async Task MixedRootsOwnershipCancellationAndSourceFallback()
    {
        var bars = Enumerable.Range(1, 40).Select(i => new Bar(default, i, i + 2, i - 1, i, 1)).ToArray();
        var prime = new PrimeNumberOscillator(3);
        await Compare([prime, new PrimeNumberBands(5), new AbsoluteStrengthIndex(3), prime], bars);
        foreach (var root in new IIndicator[] { new PrimeNumberOscillator(3).Of(new Sma(2)), new PrimeNumberBands(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars);
        }
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(prime);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(roots).ConfigureHistory(history);
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
