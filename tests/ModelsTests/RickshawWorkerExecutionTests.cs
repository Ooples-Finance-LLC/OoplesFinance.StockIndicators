using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class RickshawWorkerExecutionTests
{
    [Theory]
    [InlineData(1, 3)] [InlineData(10, 5)] [InlineData(31, 17)]
    public async Task WorkerBoundariesAndGridFallbackMatchIndependentFormula(int doji, int near)
    {
        var bars = BarsOf(1025);
        bars[127] = new(default, double.Epsilon, 8 * double.Epsilon, -8 * double.Epsilon, -0d, 1);
        bars[128] = new(default, 0, double.MaxValue, -double.MaxValue, double.Epsilon, 1);
        bars[257] = new(default, -2, -4, 5, 1, 1);
        bars[513] = new(default, -0d, 0d, -0d, 0d, 1);
        bars[767] = new(default, 1e-200, 1e-199, -1e-199, 0, 1);
        await CheckWorkers(bars, [new RickshawManCandle(doji, near)], independent: true);
    }

    [Theory]
    [InlineData(4095)] [InlineData(4096)] [InlineData(int.MaxValue)]
    public async Task ReplayLimitAndLargePeriodFallbackPreserveWarmup(int period)
    {
        var bars = Enumerable.Repeat(new Bar(default, 5, 10, 0, 5, 1), 8193).ToArray();
        await CheckWorkers(bars, [new RickshawManCandle(period, 1)], independent: false);
    }

    [Fact]
    public async Task MixedRootsRecoverFromCancellationAndLateInvalidInput()
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = BarsOf(1025);
            var first = new RickshawManCandle(10, 5);
            var second = new RickshawManCandle(3, 17);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(first, second, first).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var last = bars[^1];
                bars[^1] = new(last.Time, last.Open, last.High, last.Low, last.Close, double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                Assert.Null(builder.LastExecution);
                bars[^1] = last;
                using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
                AssertFormula(bars, first, run); AssertFormula(bars, second, run);
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    private static async Task CheckWorkers(Bar[] bars, RickshawManCandle[] indicators, bool independent)
    {
        int previous = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            {
                AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = workers;
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var input = bars.ToArray();
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                        .ConfigureIndicators(indicators).ConfigureHistory(mode).BuildAsync();
                    Array.Clear(input); run.Dispose();
                    foreach (var indicator in indicators)
                        if (independent) AssertFormula(bars, indicator, run);
                        else Assert.Equal(Enumerable.Range(0, bars.Length).Select(i => i >= indicator.WarmupBars ? 100d : 0d), run[indicator].ToArray());
                    Assert.Equal(bars[^1], run.Latest.Bar);
                    if (mode == IndicatorHistoryMode.Full)
                    {
                        int row = 0;
                        await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                        Assert.Equal(bars.Length, row);
                    }
                }
            }
        }
        finally { AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    private static void AssertFormula(Bar[] bars, RickshawManCandle indicator, IIndicatorRun run)
    {
        foreach (var rule in indicator.ValidationRules)
            rule.Check(new IndicatorValidationContext("Rickshaw worker formula", bars, [run[indicator].ToArray()], 0));
    }

    private static Bar[] BarsOf(int count) => Enumerable.Range(0, count)
        .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i % 3 == 0 ? 5 : 1, 10, 0, i % 3 == 0 ? 5 : 2, i + 1)).ToArray();
}
