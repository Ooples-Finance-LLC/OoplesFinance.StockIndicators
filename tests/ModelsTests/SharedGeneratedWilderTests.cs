using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedWilderTests
{
    private static IIndicator Create(int family, int period) => family switch
    {
        0 => new Wwma(period),
        1 => new Smma(period),
        _ => new ModifiedMa(period)
    };

    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 3)
        from period in new[] { int.MinValue, 1, 3, 14, int.MaxValue }
        select new object[] { family, period };

    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(default, i, i + 1, i - 1, p, i + 2)).ToArray();

    [Theory, MemberData(nameof(Cases))]
    public async Task AllAliasesPreserveBitsOwnershipAndHistory(int family, int period)
    {
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0d, -0d],
            [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0],
            Enumerable.Range(0, 65).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = BarsOf(prices);
            var indicator = Create(family, period);
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Builder();
            using var actual = await builder.BuildAsync();
            if (history == IndicatorHistoryMode.Full)
                Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
            var output = indicator.Outputs[0];
            AssertBits(expected[output].ToArray(), actual[output].ToArray());
            if (bars.Length > 0)
            {
                Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
            }
            actual.Dispose();
            Array.Clear(bars);
            AssertBits(expected[output].ToArray(), actual[output].ToArray());
        }
    }

    [Fact]
    public async Task MixedAndRepeatedRootsKeepIndependentStateAcrossBuilds()
    {
        IIndicator[] roots = [new Wwma(3), new Smma(7), new ModifiedMa(14), new Ema(5)];
        var bars = BarsOf(Enumerable.Range(0, 65).Select(i => i % 11 - 5d));
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators([.. roots, roots[0]]);
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(roots).ConfigureBehavior(_ => { }).BuildAsync();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            using var actual = await builder.BuildAsync();
            foreach (var root in roots)
                AssertBits(expected[root.Outputs[0]].ToArray(), actual[root.Outputs[0]].ToArray());
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ValidationCancellationAndRequiredGpuRetainContracts(int family)
    {
        var bars = BarsOf([1, 2, 3]);
        bars[^1] = new(default, 1, 2, 0, 3, double.NaN);
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(Create(family, 3));
        var expected = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
        var builder = Builder();
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
        bars[^1] = new(default, 1, 2, 0, 3, 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
    }

    private static void AssertBits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
