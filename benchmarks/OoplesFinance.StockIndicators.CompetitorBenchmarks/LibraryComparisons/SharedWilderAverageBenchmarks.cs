using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedWilderAverageBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Smma", "Skender.GetSmma", "Trady.Indicator.ModifiedMovingAverage")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _quan = null!;
    private (DateTime, double)[][] _tuples = null!;
    private decimal?[][] _decimals = null!;
    private int _priorDop;
    private bool FirstSeed => Operation.StartsWith("Trady.", StringComparison.Ordinal);
    private bool Skender => Operation == "Skender.GetSmma";

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var input = SkenderWindowBenchmarkInput.Create(Count, 0);
        _bars = input.Bars;
        _quan = input.Prices.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        var decimals = input.Prices.Select(v => (decimal?)v).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _tuples = Enumerable.Range(0, chunks).Select(i => input.Tuples[..(Count * (i + 1) / chunks)]).ToArray();
        _decimals = Enumerable.Range(0, chunks).Select(i => decimals[..(Count * (i + 1) / chunks)]).ToArray();
        var pair = WilderAverageComparison.Pairs.Single(p => p.Id == Operation);
        var data = CompetitorData.FromCloses(input.Prices);
        var exact = pair.Reference!(data, Period).Outputs["Value"].Values;
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"].Values;
        Check(NativeSingle(), native); Check(NativeParallel(), native);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create(); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(exact, run[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    private WilderMovingAverage Create() => new(Period, !FirstSeed);
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) =>
        SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);

    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        if (FirstSeed) return Trady(_decimals[^1]);
        if (Skender) return Smma(_tuples[^1]);
        var output = new double[Count]; Quan(0, Count, output); return output;
    }
    [Benchmark] public object NativeParallel()
    {
        if (FirstSeed) return Parallel(_decimals, Trady);
        if (Skender) return Parallel(_tuples, Smma);
        var output = new double[Count];
        int chunks = _tuples.Length;
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            Quan(Count * chunk / chunks, Count * (chunk + 1) / chunks, output));
        return output;
    }
    private static IReadOnlyList<decimal?> Trady(decimal?[] prices) => new ModifiedMovingAverageByTuple(prices, Period).Compute();
    private static IReadOnlyList<SmmaResult> Smma((DateTime, double)[] prices) => (IReadOnlyList<SmmaResult>)prices.GetSmma(Period);
    private T[] Parallel<TInput, T>(TInput[][] prefixes, Func<TInput[], IReadOnlyList<T>> compute)
    {
        var output = new T[Count];
        CpuParallelSettings.LightweightParallel(prefixes.Length, prefixes.Length, chunk =>
        {
            var local = compute(prefixes[chunk]);
            int start = Count * chunk / prefixes.Length;
            for (int i = start; i < local.Count; i++) output[i] = local[i];
        });
        return output;
    }
    private void Quan(int start, int end, double[] output)
    {
        var state = new QuanTAlib.Smma(Period);
        // Every recursive native path replays its complete prefix inside timing.
        for (int i = 0; i < start; i++) state.Calc(_quan[i]);
        for (int i = start; i < end; i++) output[i] = state.Calc(_quan[i]).Value;
    }
    private void Check(object result, double[] expected)
    {
        if (result is double[] values) { AsinFeasibilityBenchmarks.RequireSame(expected, values); return; }
        var nullable = result is IReadOnlyList<decimal?> decimals
            ? decimals.Select(v => v.HasValue ? (double?)v.Value : null).ToArray()
            : ((IReadOnlyList<SmmaResult>)result).Select(v => v.Smma).ToArray();
        int first = Skender ? Period - 1 : 0;
        if (nullable.Length != Count || nullable.Take(first).Any(v => v.HasValue)
            || nullable.Skip(first).Any(v => !v.HasValue))
            throw new InvalidOperationException("Unexpected Wilder startup presence.");
        AsinFeasibilityBenchmarks.RequireSame(expected[first..], nullable[first..].Select(v => v!.Value).ToArray());
    }
}
