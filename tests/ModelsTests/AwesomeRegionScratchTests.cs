using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AwesomeRegionScratchTests
{
    [Theory]
    [InlineData(1, 2)] [InlineData(5, 34)]
    public async Task ReusedScratchPreservesWarmupZeroDenominatorsAndRaggedWorkerBoundaries(int fast, int slow)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 1000, 10003 })
            {
                var bars = Data(count, 0);
                var original = bars.ToArray();
                var indicator = new AwesomeWithDetails(fast, slow);
                var expected = ReferenceState(bars, indicator);
                foreach (int workers in new[] { 1, 8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                        .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
                    AssertColumns(run, indicator, expected);
                    if (mode == IndicatorHistoryMode.Full)
                    {
                        Array.Clear(bars);
                        int index = 0;
                        await foreach (var snapshot in run) Assert.Equal(original[index++], snapshot.Bar);
                        Assert.Equal(count, index);
                        original.CopyTo(bars, 0);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ConcurrentBuildsKeepTheirWorkerScratchPrivate()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            await Task.WhenAll(Enumerable.Range(0, 4).Select(phase => Task.Run(async () =>
            {
                var bars = Data(10003, phase);
                var indicator = new AwesomeWithDetails(5, 34);
                var expected = ReferenceState(bars, indicator);
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync();
                AssertColumns(run, indicator, expected);
            })));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static Bar[] Data(int count, int phase) => Enumerable.Range(0, count).Select(i =>
    {
        // Long zero stretches make normalized output absent again after it was
        // present, so stale scratch cannot hide behind ordinary warmup values.
        double mid = i % 127 < 45 ? 0 : ((i * 13 + phase * 17) % 101 - 50) / 8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), mid, mid + 1, mid - 1, mid, i + 1);
    }).ToArray();

    private static double[][] ReferenceState(Bar[] bars, AwesomeWithDetails indicator)
    {
        var state = (IMultiOutputState)indicator.CreateState();
        var result = Enumerable.Range(0, 4).Select(_ => new double[bars.Length]).ToArray();
        var values = new double[4];
        for (int i = 0; i < bars.Length; i++)
        {
            Array.Clear(values);
            state.Update(in bars[i], values);
            for (int slot = 0; slot < 4; slot++) result[slot][i] = values[slot];
        }
        return result;
    }

    private static void AssertColumns(IIndicatorRun run, AwesomeWithDetails indicator, double[][] expected)
    {
        for (int slot = 0; slot < 4; slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}
