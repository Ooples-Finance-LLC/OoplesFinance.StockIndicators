using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class RegressionWithFinalLineBuilderTests
{
    private static Bar[] BarsFor(params double[] values) => values.Select((v, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(int.MaxValue)]
    public async Task SharedAndFallbackPathsOwnEveryOutput(int period)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool projected in new[] { false, true })
        {
            var bars = BarsFor(1, 2, 4, 9, 9, 9, 0, 2 * double.Epsilon, 4 * double.Epsilon);
            var saved = bars.ToArray();
            var expected = RegressionSnapshot.Calculate(bars, period);
            var indicator = new RegressionWithFinalLine(period);
            var builder = new StockIndicatorBuilder().ConfigureSource(projected ? Bars.From(bars, b => b) : Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            using var run = await builder.BuildAsync();
            if (!projected && history == IndicatorHistoryMode.Full)
                Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
            Array.Clear(bars);
            run.Dispose();
            var columns = new[] { expected.Select(r => r.Slope), expected.Select(r => r.Intercept),
                expected.Select(r => r.StandardDeviation), expected.Select(r => r.RSquared), expected.Select(r => r.Line) };
            for (int slot = 0; slot < 5; slot++)
            {
                Assert.Equal(columns[slot].Select(v => BitConverter.DoubleToInt64Bits(v ?? 0)),
                    run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(columns[slot].Select(v => v.HasValue ? 1d : 0), run[indicator.Outputs[slot + 5]].ToArray());
            }
            Assert.Equal(saved[^1], run.Latest.Bar);
            if (history == IndicatorHistoryMode.Full)
            {
                int index = 0;
                await foreach (var row in run) Assert.Equal(saved[index++], row.Bar);
            }
        }
    }

    [Fact]
    public async Task AppendingRepaintsOnlyNewRunAndChainingReadsCompletedSlope()
    {
        var indicator = new RegressionWithFinalLine(3);
        using var first = await Build(BarsFor(1, 2, 4), indicator);
        var bars = BarsFor(1, 2, 4, 9);
        var average = new Sma(1).Of(indicator);
        using var second = await Build(bars, indicator, average);
        Assert.Equal(new[] { 5d / 6, 7d / 3, 23d / 6 }, first[indicator.Line].ToArray());
        Assert.Equal(new[] { 0d, 1.5, 5, 8.5 }, second[indicator.Line].ToArray());
        Assert.Equal(new[] { 0d, 1, 1, 1 }, second[indicator.LineIsDefined].ToArray());
        Assert.Equal(-5.5, second[indicator.Intercept].ToArray()[3]);
        Assert.Equal(second[indicator.Slope].ToArray(), second[average].ToArray());
        using var mixed = await Build(bars, indicator, new Sma(2));
        Assert.Equal(second[indicator.Line].ToArray(), mixed[indicator.Line].ToArray());
    }

    [Fact]
    public async Task WarmupAndSelectedSourcePreserveGlobalCoordinates()
    {
        var all = BarsFor(1, 2, 4, 9, 3);
        var indicator = new RegressionWithFinalLine(3);
        using var full = await Build(all, indicator);
        using var warmed = await new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(all.Skip(2)).WarmedWith(all.Take(2)))
            .ConfigureIndicators(indicator).BuildAsync();
        for (int slot = 0; slot < 10; slot++)
            Assert.Equal(full[indicator.Outputs[slot]].ToArray().Skip(2), warmed[indicator.Outputs[slot]].ToArray());
        var selected = new RegressionWithFinalLine(3).Of(new Sma(1));
        using var chained = await Build(all, selected);
        for (int slot = 0; slot < 10; slot++)
            Assert.Equal(full[indicator.Outputs[slot]].ToArray(), chained[selected.Outputs[slot]].ToArray());
    }

    [Fact]
    public async Task ValidationRejectsEveryCorruptedColumnIndependently()
    {
        var bars = BarsFor(1, 2, 4, 9, 9, 9);
        var indicator = new RegressionWithFinalLine(3);
        using var run = await Build(bars, indicator);
        var values = indicator.Outputs.Select(o => run[o].ToArray()).ToArray();
        var rules = indicator.ValidationRules.ToArray();
        for (int slot = 0; slot < 10; slot++)
        {
            rules[slot].Check(new IndicatorValidationContext("correct", bars, values, 0));
            var corrupted = values.Select(v => v.ToArray()).ToArray();
            corrupted[slot][4] += 1;
            Assert.ThrowsAny<Exception>(() => rules[slot].Check(new IndicatorValidationContext("corrupt", bars, corrupted, 0)));
        }
        Assert.Equal(0, values[8][5]);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)]
    public async Task IndependentValidationIncludesOverflowAndAdversarialInputs(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(
            typeof(RegressionWithFinalLine), "retrospective regression", () => new RegressionWithFinalLine(period)));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    [Fact]
    public async Task EmptyHugeWindowCancellationAndValidationOrderArePreserved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegressionWithFinalLine(1));
        using var empty = await Build([], new RegressionWithFinalLine(int.MaxValue));
        var indicator = new RegressionWithFinalLine(2);
        var bars = BarsFor(-double.MaxValue, double.MaxValue, 1);
        var overflow = await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(bars, indicator));
        Assert.Equal(0, overflow.OutputSlot);
        Assert.Equal(1, overflow.BarIndex);
        bars[2] = new Bar(default, double.NaN, 1, 1, 1, 1);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(bars, indicator));
        using var startup = await Build(BarsFor(-double.MaxValue, double.MaxValue), new RegressionWithFinalLine(3));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(BarsFor(1, 2))).ConfigureIndicators(indicator).BuildAsync(cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.Live()).ConfigureIndicators(indicator).BuildAsync());
    }

    private static Task<IIndicatorRun> Build(Bar[] bars, params IIndicator[] indicators) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
}
