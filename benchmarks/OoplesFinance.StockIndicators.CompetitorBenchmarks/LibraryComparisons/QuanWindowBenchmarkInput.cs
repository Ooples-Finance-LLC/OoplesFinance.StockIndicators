using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Setup-only input preparation shared by the mode, Hull and sample-shape campaigns.
internal static class QuanWindowBenchmarkInput
{
    internal static (Bar[] Bars, QuanTAlib.TValue[] Input, CompetitorData Data) Create(int count)
    {
        var values = Enumerable.Range(0, count).Select(i => ((i * 13 + i / 37) % 31 - 15) / 8d).ToArray();
        var bars = values.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        var input = values.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        var data = CompetitorData.FromOhlc(values, values.Select(v => v + 1).ToArray(), values.Select(v => v - 1).ToArray(), values);
        return (bars, input, data);
    }
}
