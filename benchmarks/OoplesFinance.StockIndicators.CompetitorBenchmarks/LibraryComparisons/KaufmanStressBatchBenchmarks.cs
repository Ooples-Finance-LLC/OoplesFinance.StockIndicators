using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Both arms include identical fresh StockData construction and full output/signal publication.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class KaufmanStressBatchBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    private double[] _primary = null!, _market = null!;
    private DateTime[] _dates = null!;

    [GlobalSetup]
    public void Setup()
    {
        _primary = Enumerable.Range(0, Count).Select(i => 100 + Math.Sin(i * .37) * 12 + i * .01).ToArray();
        _market = Enumerable.Range(0, Count).Select(i => 100 + Math.Sin(i * .19) * 12 + i * .01).ToArray();
        _dates = Enumerable.Range(0, Count).Select(i => DateTime.UnixEpoch.AddMinutes(i)).ToArray();
        var expected = Legacy(); var actual = Shared();
        if (!expected.SignalsList.SequenceEqual(actual.SignalsList) ||
            !expected.OutputValues.Keys.SequenceEqual(actual.OutputValues.Keys) ||
            !expected.CustomValuesList.Select(BitConverter.DoubleToInt64Bits).SequenceEqual(actual.CustomValuesList.Select(BitConverter.DoubleToInt64Bits)))
            throw new InvalidOperationException("Kaufman Stress legacy parity failed.");
        foreach (var key in expected.OutputValues.Keys)
            if (!expected.OutputValues[key].Select(BitConverter.DoubleToInt64Bits).SequenceEqual(actual.OutputValues[key].Select(BitConverter.DoubleToInt64Bits)))
                throw new InvalidOperationException("Kaufman Stress output parity failed.");
    }

    private StockData Data(double[] values) => new(values, values.Select(v => v + 2), values.Select(v => v - 3), values,
        values.Select(_ => 1d), _dates);
    [Benchmark(Baseline = true)] public StockData Legacy() => PreservedKaufmanStressBatch.Run(Data(_primary), Data(_market));
    [Benchmark] public StockData Shared() => Data(_primary).CalculateKaufmanStressIndicator(Data(_market));
}
