using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Setup-only data and replay slices for Skender's tuple window APIs.
internal sealed record SkenderWindowBenchmarkInput(
    double[] Prices, Bar[] Bars, (DateTime, double)[] Tuples,
    (DateTime, double)[][] Chunks, int[] Skip)
{
    internal static SkenderWindowBenchmarkInput Create(int count, int seedCount)
    {
        var prices = Enumerable.Range(0, count).Select(i => 10 + i % 101 / 8d).ToArray();
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var tuples = bars.Select(b => (b.Time, b.Close)).ToArray();
        int workers = Math.Min(8, Environment.ProcessorCount);
        var chunks = new (DateTime, double)[workers][];
        var skip = new int[workers];
        for (int worker = 0; worker < workers; worker++)
        {
            int start = count * worker / workers, end = count * (worker + 1) / workers;
            int seed = Math.Max(0, start - seedCount);
            skip[worker] = start - seed;
            chunks[worker] = tuples[seed..end];
        }
        return new(prices, bars, tuples, chunks, skip);
    }
}
