using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedProjectionFamilyTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 3)
        from period in new[] { int.MinValue, -3, 0, 1, 2, 3, 14, int.MaxValue }
        select new object[] { family, period };
    private static IIndicator Create(int family, int period) => family switch
    {
        0 => new ProjectionBands(period), 1 => new ProjectionOscillator(period),
        2 => new ProjectionBandwidth(period), _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task LaggedBoundsRatiosRoundedSignalsAndHugePeriodsMatchLegacy(int family, int period)
    {
        Assert.True(ValuesBarExecution.SupportsOwned([Create(family, period)]));
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            [double.Epsilon, double.MaxValue, 1, 2, 3, 4, 5, 6, 7],
            [1, -1, 0, 0, 2, -2, 0, 3, -3],
            Enumerable.Range(0, 65).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 33).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        }) await Compare(Create(family, period), values);
    }

    [Fact]
    public async Task HiddenOverflowingBandsDoNotOverflowPublishedRatios()
    {
        var bars = new[] { -double.MaxValue, double.MaxValue, 0 }.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var oscillator = new ProjectionOscillator(2); var bandwidth = new ProjectionBandwidth(2);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(oscillator, bandwidth).ConfigureHistory(history).BuildAsync();
            Assert.Equal(0, run[oscillator.Value].ToArray()[2]);
            Assert.Equal(200, run[bandwidth.Value].ToArray()[2]);
        }
    }

    [Fact]
    public async Task ExplicitAverageStagesAndSelectedSourcesRetainFallback()
    {
        foreach (IIndicator root in new IIndicator[] {
            new ProjectionOscillator(3, new Sma(3)), new ProjectionBandwidth(3, new Ema(3)),
            new ProjectionOscillator(3, new Wma(3), new Sma(4)), new ProjectionBandwidth(3, new Wma(3), new Ema(3)),
            new ProjectionBands(3).Of(new Sma(2)), new ProjectionOscillator(3).Of(new Sma(2)), new ProjectionBandwidth(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare(root, [double.Epsilon, 2, -1, 0, 4, 2]);
        }
    }

    [Fact]
    public async Task MixedRootsRepeatedBuildsAndInvalidTailPreserveStateAndValidationPriority()
    {
        IIndicator[] roots = [new ProjectionBands(3), new ProjectionOscillator(3), new ProjectionBandwidth(3), new Cci(3)];
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
