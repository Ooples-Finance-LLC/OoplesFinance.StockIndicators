using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedKlingerBenchmarks
{
    private const int Fast = 34, Slow = 55, Signal = 13;
    [Params("Skender.GetKvo")] public string Operation { get; set; } = "Skender.GetKvo";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private Quote[] _quotes = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation != "Skender.GetKvo") throw new ArgumentException("Unknown Klinger operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _quotes = Enumerable.Range(0, Count).Select(i =>
        {
            decimal price = 100 + (i * 13 % 101) / 8m;
            return new Quote { Date = DateTime.UnixEpoch.AddMinutes(i), Open = price, High = price + 2,
                Low = price - 2, Close = price, Volume = 10 + i % 17 };
        }).ToArray();
        _bars = _quotes.Select(q => new Bar(q.Date, (double)q.Open, (double)q.High, (double)q.Low, (double)q.Close, (double)q.Volume)).ToArray();
        var expected = KlingerComparison.Reference(_bars, Fast, Slow, Signal);
        var nativeExpected = KlingerComparison.NativeReference(_bars, Fast, Slow, Signal);
        var serial = NativeSingle().ToArray();
        var parallel = NativeParallel();
        SharedBenchmarkSupport.RequireExactNullable(nativeExpected[0], serial.Select(r => r.Oscillator).ToArray());
        SharedBenchmarkSupport.RequireExactNullable(nativeExpected[1], serial.Select(r => r.Signal).ToArray());
        SharedBenchmarkSupport.RequireExactNullable(serial.Select(r => r.Oscillator).ToArray(), parallel.Select(r => r.Oscillator).ToArray());
        SharedBenchmarkSupport.RequireExactNullable(serial.Select(r => r.Signal).ToArray(), parallel.Select(r => r.Signal).ToArray());
        for (int slot = 0; slot < 2; slot++)
            for (int i = 0; i < Count; i++)
                if (nativeExpected[slot][i].HasValue != expected[slot][i].HasValue ||
                    expected[slot][i] is double value && Math.Abs(value - nativeExpected[slot][i]!.Value) > 1e-8)
                    throw new InvalidOperationException("Native and owned Klinger disagree on benchmark inputs.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new KlingerVolumeWithSignal(Fast, Slow, Signal);
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 2; slot++)
            {
                var values = run[indicator.Outputs[slot]].ToArray();
                var masks = run[indicator.Outputs[slot + 2]].ToArray();
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), values);
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), masks);
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(Fast, Slow, Signal), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(Fast, Slow, Signal), IndicatorHistoryMode.LatestOnly);
    // Retain the library's own result collection without an additional normalization copy.
    [Benchmark(Baseline = true)] public List<KvoResult> NativeSingle() =>
        (List<KvoResult>)_quotes.GetKvo(Fast, Slow, Signal);
    [Benchmark] public KvoResult[] NativeParallel()
    {
        var result = new KvoResult[Count];
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            // EMA recurrences require complete prefixes; native arrays, quote conversion
            // and skipped-prefix results remain inside the timed public API call.
            int index = 0;
            foreach (var value in _quotes.Take(end).GetKvo(Fast, Slow, Signal))
            {
                if (index >= start) result[index] = value;
                index++;
            }
        });
        return result;
    }
    private IIndicatorRun Build(KlingerVolumeWithSignal indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
