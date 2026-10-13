using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedReverseWilderBenchmarks
{
    private const int Period = 20;
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _ticks = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _ticks = prices.Select((p, i) => new QuanTAlib.TValue(DateTime.UnixEpoch.AddMinutes(i), p, true, false)).ToArray();
        _bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 1, p - 1, p, 1)).ToArray();
        var data = CompetitorData.FromCloses(prices);
        var pair = ReverseWilderComparison.Pair;
        var expected = pair.Reference!(data, Period).Outputs["Value"].Values;
        var nativeExpected = pair.CompetitorReference!(data, Period).Outputs["Value"].Values;
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(nativeExpected, native);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        for (int i = 0; i < Count; i++)
            if (!double.IsFinite(native[i]) || !double.IsFinite(expected[i]) || Math.Abs(expected[i] - native[i]) > 1e-10 * Math.Max(1, Math.Max(Math.Abs(expected[i]), Math.Abs(native[i]))))
                throw new InvalidOperationException($"Native and owned reverse Wilder disagree at {i}.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Outputs[0]].ToArray());
        }
    }
    private static ReverseWilderAverage Create() => new(Period);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = new double[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        var result = new double[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, double[] result)
    {
        // Complete prefix replay preserves rounded startup and extrapolation.
        // Native state allocations and discarded prefix work remain timed.
        var state = new QuanTAlib.Rma(Period);
        for (int i = 0; i < start; i++) state.Calc(_ticks[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_ticks[i]).Value;
    }
    private IIndicatorRun Build(ReverseWilderAverage indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
