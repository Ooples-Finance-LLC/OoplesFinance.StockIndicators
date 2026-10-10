using System.Diagnostics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// End-to-end diagnostic, not a replacement for BenchmarkDotNet acceptance runs.
internal static class PilotGpuSchedulingProbe
{
    internal static async Task Run()
    {
        int[] sizes = [8191, 8192, 8193, 8194, 16383, 16384, 16385, 8191];
        var sources = sizes.Select(count => Enumerable.Range(0, count)
            .Select(i => new Bar(default, .5, .5, .5, .5, 1)).ToArray()).ToArray();
        async Task Execute(int index)
        {
            var sma = new Sma(20);
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(sources[index]))
                .ConfigureIndicators(sma).ConfigureExecution(IndicatorExecutionBackend.Gpu);
            using var run = await builder.BuildAsync();
            if (builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu ||
                BitConverter.DoubleToInt64Bits(run[sma][0]) != 0L
                || BitConverter.DoubleToInt64Bits(run[sma][sizes[index] - 1]) != BitConverter.DoubleToInt64Bits(.5))
                throw new InvalidOperationException("GPU scheduling probe failed.");
        }
        for (int i = 0; i < sizes.Length; i++) await Execute(i);
        foreach (bool concurrent in new[] { false, true })
        {
            var samples = new double[9];
            for (int repeat = 0; repeat < samples.Length; repeat++)
            {
                var timer = Stopwatch.StartNew();
                if (concurrent) await Task.WhenAll(Enumerable.Range(0, sizes.Length).Select(i => Task.Run(() => Execute(i))));
                else for (int i = 0; i < sizes.Length; i++) await Execute(i);
                samples[repeat] = timer.Elapsed.TotalMilliseconds;
            }
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { concurrent, sizes, batchMilliseconds = samples }));
        }
    }
}
