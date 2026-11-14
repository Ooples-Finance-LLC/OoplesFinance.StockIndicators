using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedBasicVolumeTests
{
    private static IndicatorBase Create(int kind) => kind switch
    {
        0 => new NetVolume(14),
        1 => new Indicators.Range(14),
        _ => new MarketFacilitationIndex(14)
    };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task BothHistoriesRetainBitsOwnershipAndRepeatedBuilds(int kind)
    {
        Assert.True(ValuesBarExecution.SupportsOwned([Create(kind)]));
        foreach (var bars in new Bar[][]
        {
            [], [new(default, 1, 2, 0, 1, 3)],
            [new(default, 0, -0d, 0d, -0d, -0d), new(default, 0, 0d, -0d, 0d, 0d),
             new(default, 0, 1, -1, 1, -0d), new(default, 0, -1, 1, -1, 0d)],
            [new(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue),
             new(default, 0, double.Epsilon, 0, -double.MaxValue, double.Epsilon)],
            Enumerable.Range(0, 129).Select(i => new Bar(default, i, i % 13, -(i % 7),
                (i * 17 % 31) / 8d, i % 5 == 0 ? 0 : (i % 11) - 5)).ToArray()
        })
            await Compare([Create(kind)], bars);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void IndependentArithmeticAndReset(int kind)
    {
        var name = kind switch { 0 => "GeneratedNetVolumeState", 1 => "GeneratedRawRangeState", _ => "GeneratedMarketFacilitationState" };
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type, nonPublic: true)!;
        Bar[] bars = [new(default, 0, 6, 2, 3, 2), new(default, 0, 8, 2, 4, 3),
            new(default, 0, 4, 2, 4, 0), new(default, 0, 3, 1, 1, -2)];
        double[] expected = kind switch { 0 => [0, 3, 0, 2], 1 => [4, 6, 2, 2], _ => [2, 2, 0, -1] };
        double[] Run() => bars.Select(bar => state.Update(in bar)).ToArray();
        Bits(expected, Run());
        state.Reset();
        Bits(expected, Run());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task SourcesDuplicateRootsCancellationAndErrorsKeepContracts(int kind)
    {
        var bars = Enumerable.Range(0, 20).Select(i => new Bar(default, i, i + 1, i - 2, i % 7, i % 3)).ToArray();
        var selected = Create(kind).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        await Compare([selected], bars);
        var repeated = Create(kind);
        await Compare([repeated, Create((kind + 1) % 3), repeated], bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            bars[^1] = new(default, 1, 2, 0, 1, invalid);
            await Compare([repeated], bars);
        }
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(repeated);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).ConfigureHistory(history);
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
                        if (bars.Length > 0)
                        {
                            Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                        }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Assert.Equal(bars.Length, expected[output].ToArray().Length);
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
