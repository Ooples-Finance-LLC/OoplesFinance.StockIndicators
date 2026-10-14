using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedDynamicMomentumBenchmarks
{
    [Params("Trady.Indicator.DynamicMomentumIndex")]
    public string Operation { get; set; } = "Trady.Indicator.DynamicMomentumIndex";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private decimal?[] _prices = null!;
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation != "Trady.Indicator.DynamicMomentumIndex") throw new ArgumentException("Unknown dynamic momentum operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var values = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _prices = values.Select(v => (decimal?)(decimal)v).ToArray();
        _bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var nativeExpected = DynamicMomentumComparison.NativeReference(_prices, 5, 10, 14, 30, 5);
        var expected = DynamicMomentumComparison.Reference(values.Select(v => (double?)v).ToArray(), 5, 10, 14, 30, 5);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        if (serial.Count != Count || parallel.Length != Count) throw new InvalidOperationException("Wrong native result count.");
        for (int i = 0; i < Count; i++)
        {
            if (nativeExpected[i] != serial[i] || serial[i] != parallel[i])
                throw new InvalidOperationException("Native dynamic momentum output or presence mismatch.");
            if (serial[i].HasValue != expected[i].HasValue
                || serial[i].HasValue && Math.Abs((double)serial[i]!.Value - expected[i]!.Value) > 1e-9)
                throw new InvalidOperationException("Native and owned dynamic momentum disagree on benchmark inputs.");
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new DynamicMomentumStrength();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v ?? 0).ToArray(), run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new DynamicMomentumStrength(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new DynamicMomentumStrength(), IndicatorHistoryMode.LatestOnly);
    // The tuple API avoids candle mapping. Compute eagerly returns its List;
    // casting preserves that native result without a measured normalization copy.
    [Benchmark(Baseline = true)] public List<decimal?> NativeSingle() =>
        (List<decimal?>)new DynamicMomentumIndexByTuple(_prices, 5, 10, 14, 30, 5).Compute();
    [Benchmark]
    public decimal?[] NativeParallel()
    {
        var output = new decimal?[Count];
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            // Keep the full input prefix for period selection and Wilder seeding,
            // but request only this worker's output range through the public API.
            var local = new DynamicMomentumIndexByTuple(_prices.Take(end), 5, 10, 14, 30, 5)
                .Compute(startIndex: start, endIndex: end - 1);
            for (int i = 0; i < local.Count; i++) output[start + i] = local[i];
        });
        return output;
    }
    private IIndicatorRun Build(DynamicMomentumStrength indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
