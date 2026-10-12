using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPathRatioBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetCmo", "Trady.Indicator.EfficiencyRatio")] public string Operation { get; set; } = "Skender.GetCmo";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private bool Absolute => Operation == "Trady.Indicator.EfficiencyRatio";
    private (DateTime, double)[] _tuples = null!;
    private decimal?[] _prices = null!;
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation is not ("Skender.GetCmo" or "Trady.Indicator.EfficiencyRatio")) throw new ArgumentException("Unknown path-ratio operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        var data = CompetitorData.FromCloses(prices);
        _bars = data.IndicatorBars;
        _tuples = _bars.Select(b => (b.Time, b.Close)).ToArray();
        _prices = prices.Select(p => (decimal?)(decimal)p).ToArray();
        var pair = PathRatioComparison.Pairs.Single(p => p.Id == Operation);
        var expected = pair.Reference!(data, Period).Outputs["Value"];
        var nativeExpected = pair.CompetitorReference!(data, Period).Outputs["Value"];
        var serial = NativeSingle();
        var parallel = NativeParallel();
        for (int i = 0; i < Count; i++)
        {
            double? value;
            if (Absolute)
            {
                var single = (List<decimal?>)serial;
                var workers = (decimal?[])parallel;
                if (single.Count != Count || workers.Length != Count) throw new InvalidOperationException("Native path-ratio length differs.");
                decimal? reference = null;
                if (i >= Period)
                {
                    decimal distance = 0;
                    for (int j = i - Period + 1; j <= i; j++) distance += Math.Abs(_prices[j]!.Value - _prices[j - 1]!.Value);
                    if (distance != 0) reference = Math.Abs(_prices[i]!.Value - _prices[i - Period]!.Value) / distance;
                }
                if (single[i] != reference || single[i] != workers[i]) throw new InvalidOperationException("Native decimal efficiency ratio differs.");
                value = (double?)single[i];
            }
            else
            {
                var single = (List<CmoResult>)serial;
                var workers = (CmoResult[])parallel;
                if (single.Count != Count || workers.Length != Count) throw new InvalidOperationException("Native path-ratio length differs.");
                if (single[i].Date != _tuples[i].Item1 || workers[i].Date != _tuples[i].Item1) throw new InvalidOperationException("CMO dates differ.");
                RequireSame(single[i].Cmo, workers[i].Cmo);
                value = single[i].Cmo;
            }
            RequireSame(nativeExpected.Present![i] ? nativeExpected.Values[i] : null, value);
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            if (run.BarCount != Count) throw new InvalidOperationException("Builder path-ratio length differs.");
            var values = run[indicator.Value].ToArray();
            var presence = run[indicator.IsDefined].ToArray();
            if (values.Length != Count || presence.Length != Count) throw new InvalidOperationException("Builder path-ratio column length differs.");
            AsinFeasibilityBenchmarks.RequireSame(expected.Values.Select((v, i) => expected.Present![i] ? v : 0).ToArray(), values);
            AsinFeasibilityBenchmarks.RequireSame(expected.Present!.Select(v => v ? 1d : 0).ToArray(), presence);
        }
    }
    private static void RequireSame(double? expected, double? actual)
    {
        if (expected.HasValue != actual.HasValue || expected.HasValue &&
            BitConverter.DoubleToInt64Bits(expected.Value) != BitConverter.DoubleToInt64Bits(actual!.Value))
            throw new InvalidOperationException("Path-ratio value or presence differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    // Keep each public API's eager result; no timed normalization or extra copy.
    [Benchmark(Baseline = true)] public object NativeSingle() => Absolute
        ? (List<decimal?>)new EfficiencyRatioByTuple(_prices, Period).Compute()
        : (List<CmoResult>)_tuples.GetCmo(Period);
    [Benchmark] public object NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        if (Absolute)
        {
            var output = new decimal?[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                int start = Count * worker / workers, end = Count * (worker + 1) / workers;
                int seed = Math.Max(0, start - Period);
                var local = new EfficiencyRatioByTuple(_prices.Skip(seed).Take(end - seed), Period)
                    .Compute(startIndex: start - seed, endIndex: end - seed - 1);
                for (int i = start; i < end; i++) output[i] = local[i - start];
            });
            return output;
        }
        var rows = new CmoResult[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            int seed = Math.Max(0, start - Period);
            var local = (List<CmoResult>)_tuples.Skip(seed).Take(end - seed).GetCmo(Period);
            for (int i = start; i < end; i++) rows[i] = local[i - seed];
        });
        return rows;
    }
    internal WindowPathRatio Create() => new(Period, Absolute ? PathRatioConvention.AbsoluteFraction : PathRatioConvention.SignedPercent);
    internal IIndicatorRun Build(WindowPathRatio indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("Required GPU path ratio did not execute on the device.");
        }
        return run;
    }
}
