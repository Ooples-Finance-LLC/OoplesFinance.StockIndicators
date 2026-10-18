using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Setup-only quote fixtures and slices for current/prior OHLC window APIs.
internal sealed record SkenderOhlcWindowBenchmarkInput(CompetitorData Data, List<Quote>[] Chunks, int[] Skip)
{
    internal static SkenderOhlcWindowBenchmarkInput Create(int count, int seedCount)
    {
        var close = Enumerable.Range(0, count).Select(i => 10 + i % 101 / 8d).ToArray();
        var data = CompetitorData.FromOhlc(close, close.Select(v => v + 1).ToArray(), close.Select(v => v - 1).ToArray(), close);
        int workers = Math.Min(8, Environment.ProcessorCount);
        var chunks = new List<Quote>[workers]; var skip = new int[workers];
        for (int worker = 0; worker < workers; worker++)
        {
            int start = count * worker / workers, end = count * (worker + 1) / workers;
            int seed = Math.Max(0, start - seedCount);
            skip[worker] = start - seed;
            chunks[worker] = data.Quotes.GetRange(seed, end - seed);
        }
        return new(data, chunks, skip);
    }
}
