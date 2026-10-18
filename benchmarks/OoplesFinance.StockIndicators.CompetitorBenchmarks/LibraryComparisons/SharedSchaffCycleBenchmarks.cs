using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSchaffCycleBenchmarks
{
    private const int Cycle = 10, Fast = 23, Slow = 50;
    [Params("Skender.GetStc")] public string Operation { get; set; } = "Skender.GetStc";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private (DateTime, double)[] _input = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation != "Skender.GetStc") throw new ArgumentException("Unknown Schaff operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _input = prices.Select((price, i) => (DateTime.UnixEpoch.AddMinutes(i), price)).ToArray();
        _bars = _input.Select(p => new Bar(p.Item1, p.Item2, p.Item2, p.Item2, p.Item2, 1)).ToArray();
        var expected = SchaffCycleComparison.Reference(prices, Cycle, Fast, Slow);
        var nativeExpected = SchaffCycleComparison.NativeReference(prices, Cycle, Fast, Slow);
        var serial = NativeSingle().Select(r => r.Stc).ToArray();
        SharedBenchmarkSupport.RequireExactNullable(nativeExpected, serial);
        SharedBenchmarkSupport.RequireExactNullable(serial, NativeParallel().Select(r => r.Stc).ToArray());
        // Verify the quote overload too; time the values-only public tuple route
        // so the competitor does not pay unnecessary decimal quote conversion.
        var quotes = _input.Select(p => new Quote { Date = p.Item1, Open = (decimal)p.Item2,
            High = (decimal)p.Item2, Low = (decimal)p.Item2, Close = (decimal)p.Item2, Volume = 1 }).ToArray();
        SharedBenchmarkSupport.RequireExactNullable(serial, quotes.GetStc(Cycle, Fast, Slow).Select(r => r.Stc).ToArray());
        for (int i = 0; i < Count; i++)
            if (serial[i].HasValue != expected[i].HasValue ||
                expected[i] is double value && Math.Abs(value - serial[i]!.Value) > 1e-8)
                throw new InvalidOperationException("Native and owned Schaff disagree on benchmark inputs.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new SingleStochasticCycle(Cycle, Fast, Slow);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v ?? 0).ToArray(), run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(Cycle, Fast, Slow), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(Cycle, Fast, Slow), IndicatorHistoryMode.LatestOnly);
    // GetStc eagerly executes; retain its own collection without a normalization copy.
    [Benchmark(Baseline = true)] public List<StcResult> NativeSingle() =>
        (List<StcResult>)_input.GetStc(Cycle, Fast, Slow);
    [Benchmark] public StcResult[] NativeParallel()
    {
        var result = new StcResult[Count];
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            // MACD's recursive EMAs require complete prefixes. All nested native
            // intermediate collections and skipped-prefix work remain measured.
            int index = 0;
            foreach (var value in _input.Take(end).GetStc(Cycle, Fast, Slow))
            {
                if (index >= start) result[index] = value;
                index++;
            }
        });
        return result;
    }
    private IIndicatorRun Build(SingleStochasticCycle indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
