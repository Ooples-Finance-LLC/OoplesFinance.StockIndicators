using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSeededAdaptiveBenchmarks
{
    private const int Period = 20, Fast = 2, Slow = 30;
    [Params("QuanTAlib.Kama", "Skender.GetKama")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _ticks = null!;
    private (DateTime, double)[] _tuples = null!;
    private bool _quan;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _quan = Operation switch
        {
            "QuanTAlib.Kama" => true,
            "Skender.GetKama" => false,
            _ => throw new ArgumentException("Unknown seeded adaptive operation.")
        };
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _tuples = prices.Select((p, i) => (DateTime.UnixEpoch.AddMinutes(i), p)).ToArray();
        _ticks = _tuples.Select(p => new QuanTAlib.TValue(p.Item1, p.Item2, true, false)).ToArray();
        _bars = _tuples.Select(p => new Bar(p.Item1, p.Item2, p.Item2 + 1, p.Item2 - 1, p.Item2, 1)).ToArray();
        var expected = SeededAdaptiveComparison.ReferenceValues(prices, Period, Fast, Slow, _quan, false);
        var nativeExpected = SeededAdaptiveComparison.ReferenceValues(prices, Period, Fast, Slow, _quan, true);
        var native = Read(NativeSingle());
        var parallel = Read(NativeParallel());
        for (int slot = 0; slot < native.Length; slot++)
        {
            SharedBenchmarkSupport.RequireExactNullable(nativeExpected[slot], native[slot]);
            SharedBenchmarkSupport.RequireExactNullable(native[slot], parallel[slot]);
            for (int i = 0; i < Count; i++)
                if (native[slot][i].HasValue != expected[slot][i].HasValue || expected[slot][i] is double value
                    && (!double.IsFinite(native[slot][i]!.Value) || Math.Abs(value - native[slot][i]!.Value) > 1e-9))
                    throw new InvalidOperationException("Native and owned adaptive outputs disagree on benchmark inputs.");
        }
        if (!_quan)
        {
            var quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
            var rows = quotes.GetKama(Period, Fast, Slow).ToArray();
            SharedBenchmarkSupport.RequireExactNullable(native[0], rows.Select(r => r.Kama).ToArray());
            SharedBenchmarkSupport.RequireExactNullable(native[1], rows.Select(r => r.ER).ToArray());
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 2; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 2]].ToArray());
            }
        }
    }
    private static double?[][] Read(object result) => result switch
    {
        double[] values => [values.Select(v => (double?)v).ToArray()],
        List<KamaResult> rows => [rows.Select(v => v.Kama).ToArray(), rows.Select(v => v.ER).ToArray()],
        KamaResult[] rows => [rows.Select(v => v.Kama).ToArray(), rows.Select(v => v.ER).ToArray()],
        _ => throw new InvalidOperationException("Unexpected native adaptive result type.")
    };
    private SeededAdaptiveAverage Create() => new(Period, Fast, Slow, _quan, _quan, !_quan);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        if (!_quan) return (List<KamaResult>)_tuples.GetKama(Period, Fast, Slow);
        var result = new double[Count];
        Calculate(0, Count, result);
        return result;
    }
    [Benchmark] public object NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        if (!_quan)
        {
            var result = new KamaResult[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                int start = Count * worker / workers, end = Count * (worker + 1) / workers;
                int index = 0;
                foreach (var value in _tuples.Take(end).GetKama(Period, Fast, Slow))
                {
                    if (index >= start) result[index] = value;
                    index++;
                }
            });
            return result;
        }
        else
        {
            var result = new double[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
                Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
            return result;
        }
    }
    private void Calculate(int start, int end, double[] result)
    {
        // Preserve recursive average and rolling-window state by replaying
        // complete prefixes. All native state allocations and replay stay timed.
        var state = new QuanTAlib.Kama(Period, Fast, Slow);
        for (int i = 0; i < start; i++) state.Calc(_ticks[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_ticks[i]).Value;
    }
    private IIndicatorRun Build(SeededAdaptiveAverage indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
