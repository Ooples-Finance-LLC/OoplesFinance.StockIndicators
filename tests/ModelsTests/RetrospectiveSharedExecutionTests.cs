using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class RetrospectiveSharedExecutionTests
{
    [Theory]
    [InlineData(2, 2, false)] [InlineData(3, 5, false)] [InlineData(5, 2, true)]
    [InlineData(int.MaxValue, 2, false)] [InlineData(2, int.MaxValue, true)]
    public async Task AllCenterValuesAndPresenceSurviveOwnedHistory(int left, int right, bool close)
    {
        foreach (int count in new[] { 0, 1, 4, 5, 8, 9, 41, 129 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                i, (i * 17 % 23) - 10d, (i * 13 % 29) - 20d, i % 9 - 4d, i + 1)).ToArray();
            await Check(bars, new(left, right, close));
        }
    }

    [Fact]
    public async Task StrictTiesSimultaneousExtremaAndSignedZeroArePreserved()
    {
        foreach (double scale in new[] { 1d, double.Epsilon, double.MaxValue / 8 })
        {
            double[] high = [-2, -1, -0d, -1, -2, 3, 3, 2, 1, 0, 4, 0, 1];
            var bars = high.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
                p * scale, -p * scale, p * scale, 1)).ToArray();
            await Check(bars, new(2, 2));
            await Check(bars, new(2, 2, true));
        }
    }

    [Fact]
    public async Task FutureWingCanInvalidateEarlierCenterAndMixedRootsRecover()
    {
        var bars = Enumerable.Range(0, 9).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1,
            i == 4 ? 9 : 1, i == 4 ? -9 : -1, i, 1)).ToArray();
        var first = new RetrospectiveFractals(2, 2); var second = new RetrospectiveFractals(2, 3, true);
        await Check(bars, first);
        using (var before = await Build(bars, first)) Assert.Equal(1, before[first.BearIsDefined].ToArray()[4]);
        bars[6] = new(bars[6].Time, 1, 9, -1, 6, 1);
        using (var after = await Build(bars, first)) Assert.Equal(0, after[first.BearIsDefined].ToArray()[4]);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(first, second, first).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            foreach (var indicator in new[] { first, second }) AssertColumns(bars, indicator, run);
            var last = bars[^1];
            bars[^1] = new(last.Time, last.Open, last.High, last.Low, last.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = last;
        }
        await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first).ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static Task<IIndicatorRun> Build(Bar[] bars, RetrospectiveFractals indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();

    [Fact]
    public async Task RetrospectiveIndicatorStillRejectsLiveSources()
    {
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var feed = Bars.Live();
            await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder()
                .ConfigureSource(feed).ConfigureIndicators(new RetrospectiveFractals())
                .ConfigureHistory(mode).BuildAsync());
        }
    }

    private static async Task Check(Bar[] bars, RetrospectiveFractals indicator)
    {
        Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input); run.Dispose(); AssertColumns(bars, indicator, run);
            if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode == IndicatorHistoryMode.Full)
            {
                int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }
    private static void AssertColumns(Bar[] bars, RetrospectiveFractals indicator, IIndicatorRun run)
    {
        var expected = new[] { new double[bars.Length], new double[bars.Length], new double[bars.Length], new double[bars.Length] };
        for (int i = indicator.LeftSpan; i < (long)bars.Length - indicator.RightSpan; i++)
        {
            double high = indicator.UseClose ? bars[i].Close : bars[i].High;
            double low = indicator.UseClose ? bars[i].Close : bars[i].Low;
            bool bear = true, bull = true;
            for (int j = i - indicator.LeftSpan; j <= i + indicator.RightSpan; j++)
                if (j != i)
                {
                    bear &= high > (indicator.UseClose ? bars[j].Close : bars[j].High);
                    bull &= low < (indicator.UseClose ? bars[j].Close : bars[j].Low);
                }
            expected[0][i] = bear ? high : 0; expected[1][i] = bull ? low : 0;
            expected[2][i] = bear ? 1 : 0; expected[3][i] = bull ? 1 : 0;
        }
        for (int slot = 0; slot < 4; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}
