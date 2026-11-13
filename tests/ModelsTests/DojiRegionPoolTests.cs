using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class DojiRegionPoolTests
{
    [Fact]
    public async Task WorkerBoundariesPreserveOriginalArithmeticStartupAndOwnedHistory()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var bars = Data(10003, 0);
            foreach (int kind in Enumerable.Range(0, 5))
            foreach (int period in new[] { 1, 2, 10, 17 })
            {
                var indicator = Create(kind, period);
                var expected = Original(bars, indicator);
                Assert.Contains(100d, expected.Skip(indicator.WarmupBars));
                Assert.Contains(0d, expected.Skip(indicator.WarmupBars));
                foreach (int workers in new[] { 1, 8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    var input = bars.ToArray();
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                        .ConfigureIndicators(indicator).ConfigureHistory(mode)
                        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
                    Array.Clear(input);
                    AssertOutput(run, indicator, expected);
                    Assert.Equal(bars[^1], run.Latest.Bar);
                    if (mode == IndicatorHistoryMode.Full)
                    {
                        int index = 0;
                        await foreach (var snapshot in run) Assert.Equal(bars[index++], snapshot.Bar);
                        Assert.Equal(bars.Length, index);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ConcurrentMixedGraphsKeepRegionStatesPrivate()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            await Task.WhenAll(Enumerable.Range(0, 4).Select(phase => Task.Run(async () =>
            {
                var bars = Data(10003, phase);
                var roots = Enumerable.Range(0, 5).Select(k => Create(k, k + 2)).ToArray();
                var expected = roots.Select(root => Original(bars, root)).ToArray();
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                        .ConfigureIndicators(roots).ConfigureHistory(mode)
                        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
                    for (int k = 0; k < roots.Length; k++) AssertOutput(run, roots[k], expected[k]);
                }
            })));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task LargePeriodsAndParallelInputFailuresRetainFallbackAndRecovery()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Data(10003, 0);
            foreach (int kind in Enumerable.Range(0, 5))
            {
                var indicator = Create(kind, kind == 1 ? int.MaxValue - 1 : int.MaxValue);
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
                AssertOutput(run, indicator, Original(bars, indicator));
            }
            var roots = Enumerable.Range(0, 5).Select(k => Create(k, 10)).ToArray();
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(roots).ConfigureHistory(mode).ConfigureExecution(IndicatorExecutionBackend.Cpu);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var saved = bars[5001];
                bars[5001] = new(DateTime.UnixEpoch.AddMinutes(5001), saved.Open, saved.High, saved.Low, saved.Close, double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                bars[5001] = saved;
                using var run = await builder.BuildAsync();
                foreach (var root in roots) AssertOutput(run, root, Original(bars, root));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static IndicatorBase Create(int kind, int period) => kind switch
    {
        0 => new AverageRangeDoji(period), 1 => new AverageRangeDragonflyDoji(period),
        2 => new AverageRangeGravestoneDoji(period), 3 => new AverageRangeLongLeggedDoji(period),
        _ => new TakuriLineCandle(period)
    };

    private static Bar[] Data(int count, int phase) => Enumerable.Range(0, count).Select(i =>
    {
        double scale = (i % 257) switch { 0 => double.MaxValue / 128, 1 => double.Epsilon, 2 => -0d, _ => .125 };
        double mid = ((i * 37 + phase * 11) % 101 - 50) * scale;
        double open = mid, close = mid + (i % 3 - 1) * scale;
        if (i % 5 == 0) open = close = mid + 4 * scale;
        else if (i % 5 == 1) open = close = mid - 5 * scale;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), open, mid + 4 * scale, mid - 5 * scale, close, i + 1);
    }).ToArray();

    private static double[] Original(Bar[] bars, IndicatorBase indicator)
    {
        var state = (IIndicatorState)indicator.CreateState();
        return bars.Select(bar => state.Update(in bar)).ToArray();
    }

    private static void AssertOutput(IIndicatorRun run, IndicatorBase indicator, double[] expected) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
            run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
}
