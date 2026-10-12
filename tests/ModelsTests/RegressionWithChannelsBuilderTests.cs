using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RegressionWithChannelsBuilderTests
{
    private static Bar[] Prices(params double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, 1)).ToArray();
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(int.MaxValue)] [InlineData(null)]
    public async Task BothFinitePathsPreserveChannelsPresenceAndOwnedResults(int? period)
    {
        foreach (bool projected in new[] { false, true })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = Prices(1, 3, 5, 10, 20, 30, 30, 30, 30);
            var saved = bars.ToArray();
            var expected = RegressionChannelSnapshot.Calculate(bars, period, 1.5);
            var indicator = new RegressionWithChannels(period, 1.5);
            var builder = new StockIndicatorBuilder().ConfigureSource(projected ? Bars.From(bars, b => b) : Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            if (!projected && history == IndicatorHistoryMode.Full) Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
            Array.Clear(bars); run.Dispose();
            var columns = new[] { expected.Select(v => v.Centerline), expected.Select(v => v.UpperChannel), expected.Select(v => v.LowerChannel) };
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.Equal(columns[slot].Select(v => BitConverter.DoubleToInt64Bits(v ?? 0)),
                    run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(columns[slot].Select(v => v.HasValue ? 1d : 0), run[indicator.Outputs[slot + 4]].ToArray());
            }
            Assert.Equal(expected.Select(v => v.BreakPoint ? 1d : 0), run[indicator.BreakPoint].ToArray());
            Assert.Equal(saved[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0;
                await foreach (var row in run) Assert.Equal(saved[index++], row.Bar);
            }
        }
    }

    [Fact]
    public async Task AppendingRealignsBlocksAndSelectedSourcesAndWarmupUseCompleteHistory()
    {
        var indicator = new RegressionWithChannels(2, 1);
        using var first = await Build(Prices(1, 3, 5, 10, 20), indicator);
        var all = Prices(1, 3, 5, 10, 20, 30);
        using var next = await Build(all, indicator, new Sma(1));
        Assert.Equal(new[] { 0d, 1, 0, 1, 0 }, first[indicator.BreakPoint].ToArray());
        Assert.Equal(new[] { 1d, 0, 1, 0, 1, 0 }, next[indicator.BreakPoint].ToArray());
        var selected = new RegressionWithChannels(2, 1).Of(new Sma(1));
        using var chained = await Build(all, selected);
        using var warmed = await new StockIndicatorBuilder().ConfigureSource(Bars.From(all.Skip(1)).WarmedWith(all.Take(1)))
            .ConfigureIndicators(indicator).BuildAsync();
        for (int slot = 0; slot < 7; slot++)
        {
            Assert.Equal(next[indicator.Outputs[slot]].ToArray(), chained[selected.Outputs[slot]].ToArray());
            Assert.Equal(next[indicator.Outputs[slot]].ToArray().Skip(1), warmed[indicator.Outputs[slot]].ToArray());
        }
    }

    [Theory]
    [InlineData(2)] [InlineData(3)]
    public async Task IndependentReferencesCoverEveryOutputAndOverflow(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(typeof(RegressionWithChannels),
            "retrospective channels", () => new RegressionWithChannels(period, 1.5)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
        var indicator = new RegressionWithChannels(period);
        var bars = Prices(1, 3, 5, 8, 2, 9);
        using var run = await Build(bars, indicator);
        var columns = indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
        var rules = indicator.ValidationRules.ToArray();
        for (int slot = 0; slot < 7; slot++)
        {
            rules[slot].Check(new IndicatorValidationContext("correct", bars, columns, 0));
            var corrupted = columns.Select(c => c.ToArray()).ToArray(); corrupted[slot][4] += 1;
            Assert.ThrowsAny<Exception>(() => rules[slot].Check(new IndicatorValidationContext("corrupt", bars, corrupted, 0)));
        }
    }

    [Fact]
    public async Task InvalidInputsPrecedeOutputOverflowAndWholeInputRequiresTwoBars()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegressionWithChannels(1));
        foreach (double multiplier in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegressionWithChannels(2, multiplier));
        using var empty = await Build([], new RegressionWithChannels(2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build([], new RegressionWithChannels(null)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(Prices(1), new RegressionWithChannels(null)));
        var bars = Prices(-double.MaxValue, double.MaxValue, 1, 2);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(bars, new RegressionWithChannels(2)));
        bars[^1] = new Bar(default, double.NaN, 1, 1, 1, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(bars, new RegressionWithChannels(2)));
        await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.Live())
            .ConfigureIndicators(new RegressionWithChannels()).BuildAsync());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(Prices(1, 2)))
            .ConfigureIndicators(new RegressionWithChannels()).BuildAsync(cancel.Token));
    }

    [Fact]
    public void BlockPublicationHonorsCancellationBeforeWritingEarlierRows()
    {
        using var writer = new RegressionWithChannels.Writer(3, 2, 3);
        var bars = Prices(1, 2, 4);
        var values = Enumerable.Range(0, 7).Select(_ => new double[3]).ToArray();
        var scratch = new double[10];
        writer.Append(bars[0], 0, values, scratch, default);
        writer.Append(bars[1], 1, values, scratch, default);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => writer.Append(bars[2], 2, values, scratch, cancel.Token));
        Assert.All(values.SelectMany(v => v), value => Assert.Equal(0, value));
    }

    private static Task<IIndicatorRun> Build(Bar[] bars, params IIndicator[] indicators) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
}
