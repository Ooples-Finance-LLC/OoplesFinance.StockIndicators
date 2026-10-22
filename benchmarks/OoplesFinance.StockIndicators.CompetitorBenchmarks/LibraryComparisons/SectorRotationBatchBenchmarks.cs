using System.Reflection;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SectorRotationBatchBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    private delegate Dictionary<string, List<double>> Runner(IReadOnlyList<double> primary, IReadOnlyList<double> market,
        MovingAvgType average, int first, int second);
    private Runner _legacy = null!, _candidate = null!;
    private double[] _primary = null!, _market = null!;

    [GlobalSetup]
    public void Setup()
    {
        _primary = Enumerable.Range(0, Count).Select(i => 100 + Math.Sin(i * .37) * 12).ToArray();
        _market = Enumerable.Range(0, Count).Select(i => 100 + Math.Sin(i * .19) * 12).ToArray();
        _legacy = (p, b, average, first, second) => PreservedSectorRotationBatch.Run(p, b,
            (pk, bk) => new SectorRotationModelState(pk, bk, average, first, second));
        var type = typeof(SectorRotationModelState).Assembly.GetType("OoplesFinance.StockIndicators.Helpers.SectorRotationBatch", true)!;
        _candidate = (Runner)type.GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate(typeof(Runner));
        var expected = Legacy(); var actual = Shared();
        if (!expected.Keys.SequenceEqual(actual.Keys)) throw new InvalidOperationException("Sector output keys changed.");
        foreach (var key in expected.Keys)
            if (actual[key].Count != Count || !expected[key].Select(BitConverter.DoubleToInt64Bits).SequenceEqual(actual[key].Select(BitConverter.DoubleToInt64Bits)))
                throw new InvalidOperationException("Sector output parity failed.");
    }

    [Benchmark(Baseline = true)] public Dictionary<string, List<double>> Legacy() => _legacy(_primary, _market, MovingAvgType.ExponentialMovingAverage, 25, 75);
    [Benchmark] public Dictionary<string, List<double>> Shared() => _candidate(_primary, _market, MovingAvgType.ExponentialMovingAverage, 25, 75);
}
