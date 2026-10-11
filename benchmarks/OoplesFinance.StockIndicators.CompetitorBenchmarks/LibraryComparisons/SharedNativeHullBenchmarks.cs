using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedNativeHullBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetHma", "Trady.Indicator.HullMovingAverage")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private (DateTime, double)[] _tuples = null!;
    private decimal?[] _decimals = null!;
    private (DateTime, double)[][] _tupleChunks = null!;
    private decimal?[][] _decimalChunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    private bool Skender => Operation == "Skender.GetHma";
    private HullWindow Create() => new(Period, Skender ? HullWindowConvention.FullWindowFloor : HullWindowConvention.FullWindowRounded);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => ((i * 13 + i / 37) % 31 - 15) / 8d).ToArray();
        _bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        _tuples = _bars.Select(b => (b.Time, b.Close)).ToArray();
        _decimals = prices.Select(v => (decimal?)v).ToArray();
        int workers = Math.Min(8, Environment.ProcessorCount), seedCount = Period + Create().RootPeriod - 2;
        _tupleChunks = new (DateTime, double)[workers][];
        _decimalChunks = new decimal?[workers][];
        _skip = new int[workers];
        for (int worker = 0; worker < workers; worker++)
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            int seed = Math.Max(0, start - seedCount);
            _skip[worker] = start - seed;
            _tupleChunks[worker] = _tuples[seed..end];
            _decimalChunks[worker] = _decimals[seed..end];
        }
        var pair = HullComparison.Create(Create().Convention);
        var data = CompetitorData.FromCloses(prices);
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"];
        CheckNative(NativeSingle(), native.Values, native.Present!);
        CheckNative(NativeParallel(), native.Values, native.Present!);
        var exact = pair.Reference!(data, Period).Outputs["Value"];
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var hull = Create(); using var run = Build(hull, history);
            AsinFeasibilityBenchmarks.RequireSame(exact.Values.Select((v, i) => exact.Present![i] ? v : 0).ToArray(), run[hull.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(exact.Present!.Select(v => v ? 1d : 0).ToArray(), run[hull.IsDefined].ToArray());
        }
    }
    private static void CheckNative(object result, double[] expected, bool[] present)
    {
        double?[] values = result is IReadOnlyList<HmaResult> skender
            ? skender.Select(r => r.Hma).ToArray()
            : ((IReadOnlyList<decimal?>)result).Select(v => (double?)v).ToArray();
        AsinFeasibilityBenchmarks.RequireSame(present.Select(v => v ? 1d : 0).ToArray(), values.Select(v => v.HasValue ? 1d : 0).ToArray());
        AsinFeasibilityBenchmarks.RequireSame(expected.Select((v, i) => present[i] ? v : 0).ToArray(), values.Select(v => v ?? 0).ToArray());
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    // Object retains the native eager collection itself: no timed mapping to doubles,
    // nullable-wrapper conversion, quote conversion, or unnecessary result materialization.
    [Benchmark(Baseline = true)] public object NativeSingle() => Skender
        ? (IReadOnlyList<HmaResult>)_tuples.GetHma(Period)
        : new HullMovingAverageByTuple(_decimals, Period).Compute();
    [Benchmark] public object NativeParallel()
    {
        int workers = _skip.Length;
        if (Skender)
        {
            var result = new HmaResult[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                var local = (IReadOnlyList<HmaResult>)_tupleChunks[worker].GetHma(Period);
                int start = Count * worker / workers;
                for (int i = _skip[worker]; i < local.Count; i++) result[start + i - _skip[worker]] = local[i];
            });
            return result;
        }
        var decimals = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            var local = new HullMovingAverageByTuple(_decimalChunks[worker], Period).Compute();
            int start = Count * worker / workers;
            for (int i = _skip[worker]; i < local.Count; i++) decimals[start + i - _skip[worker]] = local[i];
        });
        return decimals;
    }
    private IIndicatorRun Build(IIndicator hull, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(hull).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
