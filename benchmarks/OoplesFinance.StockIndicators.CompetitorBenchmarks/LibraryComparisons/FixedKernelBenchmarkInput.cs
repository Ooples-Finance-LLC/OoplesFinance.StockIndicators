using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Shared setup only: measured methods continue to use their existing fields.
internal static class FixedKernelBenchmarkInput
{
    internal static double[] Create(int count, out Bar[] bars, out QuanTAlib.TValue[] native, out int priorDop)
    {
        priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var values = Enumerable.Range(0, count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        bars = values.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        native = values.Select(v => new QuanTAlib.TValue(v, true, false)).ToArray();
        return values;
    }
}
