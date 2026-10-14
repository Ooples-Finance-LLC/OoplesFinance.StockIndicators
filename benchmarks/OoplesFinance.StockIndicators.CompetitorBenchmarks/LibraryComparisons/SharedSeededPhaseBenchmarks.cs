using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSeededPhaseBenchmarks
{
    [Params("QuanTAlib.Mama", "Skender.GetMama")] public string Operation { get; set; } = "";
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
            "QuanTAlib.Mama" => true,
            "Skender.GetMama" => false,
            _ => throw new ArgumentException("Unknown seeded phase operation.")
        };
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var closes = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _bars = closes.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 2, p - 1, p, 1)).ToArray();
        _ticks = _bars.Select(b => new QuanTAlib.TValue(b.Time, b.Close, true, false)).ToArray();
        // Midpoint differs from close; dyadic fixture prices make this exact.
        _tuples = _bars.Select(b => (b.Time, (b.High + b.Low) / 2)).ToArray();
        var input = _quan ? closes : _tuples.Select(t => t.Item2).ToArray();
        var expected = SeededPhaseComparison.GridReference(input, .5, .05, _quan);
        var nativeExpected = SeededPhaseComparison.NativeReference(input, .5, .05, _quan);
        var native = Read(NativeSingle());
        var parallel = Read(NativeParallel());
        for (int slot = 0; slot < 2; slot++)
        {
            SharedBenchmarkSupport.RequireExactNullable(nativeExpected[slot], native[slot]); SharedBenchmarkSupport.RequireExactNullable(native[slot], parallel[slot]);
            for (int i = 0; i < Count; i++)
                if (native[slot][i].HasValue != expected[slot][i].HasValue || expected[slot][i] is double value
                    && (!double.IsFinite(native[slot][i]!.Value) || Math.Abs(value - native[slot][i]!.Value) > 1e-9))
                    throw new InvalidOperationException($"Native and owned seeded phase differ at {slot}/{i}.");
        }
        if (!_quan)
        {
            var quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
            var rows = quotes.GetMama().ToArray();
            SharedBenchmarkSupport.RequireExactNullable(native[0], rows.Select(r => r.Mama).ToArray());
            SharedBenchmarkSupport.RequireExactNullable(native[1], rows.Select(r => r.Fama).ToArray());
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
    private double?[][] Read(object result) => result switch
    {
        double[] values => [values.Take(Count).Select(v => (double?)v).ToArray(), values.Skip(Count).Select(v => (double?)v).ToArray()],
        List<MamaResult> rows => [rows.Select(v => v.Mama).ToArray(), rows.Select(v => v.Fama).ToArray()],
        MamaResult[] rows => [rows.Select(v => v.Mama).ToArray(), rows.Select(v => v.Fama).ToArray()],
        _ => throw new InvalidOperationException("Unexpected native seeded phase result type.")
    };
    private SeededPhaseAdaptiveAverage Create() => new(.5, .05, _quan, _quan, !_quan);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        if (!_quan) return (List<MamaResult>)_tuples.GetMama();
        var result = new double[2 * Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public object NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        if (!_quan)
        {
            var result = new MamaResult[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                int start = Count * worker / workers, end = Count * (worker + 1) / workers, index = 0;
                foreach (var value in _tuples.Take(end).GetMama())
                {
                    if (index >= start) result[index] = value;
                    index++;
                }
            });
            return result;
        }
        else
        {
            var result = new double[2 * Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
                Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
            return result;
        }
    }
    private void Calculate(int start, int end, double[] result)
    {
        // Replay complete prefixes to preserve Hilbert history and both adaptive
        // recurrences. State allocation and discarded prefix work remain timed.
        var state = new QuanTAlib.Mama();
        for (int i = 0; i < start; i++) state.Calc(_ticks[i]);
        for (int i = start; i < end; i++)
        {
            result[i] = state.Calc(_ticks[i]).Value; result[Count + i] = state.Fama.Value;
        }
    }
    private IIndicatorRun Build(SeededPhaseAdaptiveAverage indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
