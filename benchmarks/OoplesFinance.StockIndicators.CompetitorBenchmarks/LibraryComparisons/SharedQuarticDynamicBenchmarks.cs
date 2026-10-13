using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedQuarticDynamicBenchmarks
{
    private const int Period = 20;
    private const double Factor = .6;
    [Params("QuanTAlib.Mgdi", "Skender.GetDynamic")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _ticks = null!;
    private (DateTime, double)[] _tuples = null!;
    private bool _reset;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _reset = Operation switch
        {
            "QuanTAlib.Mgdi" => false,
            "Skender.GetDynamic" => true,
            _ => throw new ArgumentException("Unknown quartic dynamic operation.")
        };
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _tuples = prices.Select((p, i) => (DateTime.UnixEpoch.AddMinutes(i), p)).ToArray();
        _ticks = _tuples.Select(p => new QuanTAlib.TValue(p.Item1, p.Item2, true, false)).ToArray();
        _bars = _tuples.Select(p => new Bar(p.Item1, p.Item2, p.Item2 + 1, p.Item2 - 1, p.Item2, 1)).ToArray();
        var expected = McGinleyComparison.GridReference(prices, Period, Factor, _reset);
        var nativeExpected = McGinleyComparison.NativeReference(prices, Period, Factor, _reset);
        var native = Read(NativeSingle());
        Require(nativeExpected, native);
        Require(native, Read(NativeParallel()));
        if (_reset)
        {
            var quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
            Require(native, quotes.GetDynamic(Period, Factor).Select(r => r.Dynamic).ToArray());
        }
        for (int i = 0; i < Count; i++)
            if (native[i].HasValue != expected[i].HasValue || expected[i] is double value
                && (!double.IsFinite(native[i]!.Value) || Math.Abs(value - native[i]!.Value) > 1e-9))
                throw new InvalidOperationException("Native and owned quartic outputs disagree on benchmark inputs.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v ?? 0).ToArray(), run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }

    private static double?[] Read(object result) => result switch
    {
        double[] values => values.Select(v => (double?)v).ToArray(),
        List<DynamicResult> values => values.Select(v => v.Dynamic).ToArray(),
        DynamicResult[] values => values.Select(v => v.Dynamic).ToArray(),
        _ => throw new InvalidOperationException("Unexpected native result type.")
    };
    private static void Require(double?[] expected, double?[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Quartic output length differs.");
        for (int i = 0; i < expected.Length; i++)
            if (expected[i].HasValue != actual[i].HasValue || expected[i] is double value
                && BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(actual[i]!.Value))
                throw new InvalidOperationException($"Quartic value/presence differs at {i}.");
    }
    private QuarticDynamicAverage Create() => new(Period, Factor,
        _reset ? McGinleyStartup.ResetDelay : McGinleyStartup.Immediate);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        // Both routes return their eager native result without normalization.
        if (_reset) return (List<DynamicResult>)_tuples.GetDynamic(Period, Factor);
        var result = new double[Count];
        Calculate(0, Count, result);
        return result;
    }
    [Benchmark] public object NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        if (_reset)
        {
            var result = new DynamicResult[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                int start = Count * worker / workers, end = Count * (worker + 1) / workers;
                int index = 0;
                foreach (var value in _tuples.Take(end).GetDynamic(Period, Factor))
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
        // The recurrence needs the complete prefix. Include replay and native
        // state allocations in each worker, rather than reseeding a partition.
        var state = new QuanTAlib.Mgdi(Period, Factor);
        for (int i = 0; i < start; i++) state.Calc(_ticks[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_ticks[i]).Value;
    }
    private IIndicatorRun Build(QuarticDynamicAverage indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
