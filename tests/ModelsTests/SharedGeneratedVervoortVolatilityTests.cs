using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedVervoortVolatilityTests
{
    public static IEnumerable<object[]> Cases =>
        from first in new[] { int.MinValue, -1, 0, 1, 2, 8, int.MaxValue }
        from second in new[] { 1, 13, int.MaxValue }
        select new object[] { first, second };

    [Theory, MemberData(nameof(Cases))]
    public async Task RoundedAverageStagesObservedStartupAndHugePeriodsMatchLegacy(int first, int second)
    {
        var root = new VervoortVolatilityBands(first, second);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            [double.Epsilon, double.MaxValue, 1, 2, 3, 4, 5, 6, 7],
            [1, -1, 0, 0, 2, -2, 0, 3, -3],
            Enumerable.Range(0, 65).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 33).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        }) await Compare(root, values);
    }

    [Fact]
    public async Task OriginalLowAndPreviousPriceControlBothRangeBranches()
    {
        var prices = new[] { 2d, 1, 3 }; var lows = new[] { 1d, 0, 2 };
        var bars = prices.Select((v, i) => new Bar(default, 99, 100, lows[i], v, 1)).ToArray();
        var root = new VervoortVolatilityBands(1, 1, 1, .5);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureHistory(history).BuildAsync();
            Bits([4, 3, 6], run[root.UpperBand].ToArray());
            Bits([2, 1, 3], run[root.Value].ToArray());
            Bits([1, 0, 1.5], run[root.LowerBand].ToArray());
        }
    }

    [Fact]
    public async Task ExtremeFactorsAndExplicitAverageStagesRetainArithmeticAndFallback()
    {
        foreach (var factor in new[] { -2d, 0, double.Epsilon, 2, double.MaxValue })
            await Compare(new VervoortVolatilityBands(2, 3, factor, -factor), [1, -1, 2, 0, 3, -2]);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var root in new[] { new VervoortVolatilityBands(devMult: invalid), new VervoortVolatilityBands(lowBandMult: invalid) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare(root, [1, 2, 3]);
        }
        foreach (IIndicator root in new IIndicator[] {
            new VervoortVolatilityBands(3, 2, maType: new Wma(3)),
            new VervoortVolatilityBands(3, 2, maType: new Wma(3), secondAverage: new Sma(3), thirdAverage: new Ema(3)),
            new VervoortVolatilityBands(3, 2).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare(root, [double.Epsilon, 2, -1, 0, 4, 2]);
        }
    }

    [Fact]
    public async Task MixedRootsRepeatedBuildsAndInvalidTailPreserveStateAndValidationPriority()
    {
        IIndicator[] roots = [new VervoortVolatilityBands(3, 2), new Cci(3)];
        var bars = Enumerable.Range(0, 33).Select(i => new Bar(default, 0, i % 7 + 1, -i % 11 - 1, i % 5 - 2d, 1)).ToArray();
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators([.. roots, roots[0]]);
        using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder();
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var actual = await builder.BuildAsync();
            foreach (var root in roots) foreach (var output in root.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
        }
        bars[0] = new(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1);
        bars[1] = bars[0]; bars[^1] = new(default, 0, 1, -1, 0, double.NaN);
        var expectedError = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
        var actualError = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.IsType<ArgumentOutOfRangeException>(expectedError);
        Assert.Equal(expectedError!.GetType(), actualError!.GetType()); Assert.Equal(expectedError.Message, actualError.Message);
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
    }

    private static async Task Compare(IIndicator root, double[] values)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select(v => new Bar(default, v / 4, v, v / 2, v, 1)).ToArray();
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType()); Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                Assert.NotNull(expected); Assert.NotNull(actual);
                if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned([root])) Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                foreach (var output in root.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
                if (bars.Length > 0) { Assert.Equal(expected.Latest.Bar, actual.Latest.Bar); Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp); }
                actual.Dispose(); Array.Clear(bars);
                foreach (var output in root.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
