using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedCloseExtremeBenchmarks
{
    private const int Period = 20;
    [Params("Min", "Max")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _inputs = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var close = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _bars = close.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        _inputs = close.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        // A scan oracle includes partial-window startup independently of either deque.
        var exact = close.Select((_, i) =>
        {
            var window = close.AsSpan(Math.Max(0, i - Period + 1), Math.Min(i + 1, Period));
            double value = window[0];
            foreach (double next in window)
                if (Operation == "Max" ? next > value : next < value) value = next;
            return value;
        }).ToArray();
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(exact, native);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        SharedWindowBenchmarkValidation.Cpu(exact, Create, (indicator, history, reference) => Build(indicator, history, reference));
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        Native(0, Count, result);
        return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            Native(start, end, result);
        });
        return result;
    }
    private void Native(int start, int end, double[] result)
    {
        // QuanTAlib 1.0.0 exposes Calc(TValue). Zero decay is window-bounded.
        // Replay only P-1 inputs and write disjoint outputs without adapters.
        var state = CreateNative();
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_inputs[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_inputs[i]).Value;
    }
    private QuanTAlib.AbstractBase CreateNative() => Operation == "Max"
        ? new QuanTAlib.Max(Period, 0) : new QuanTAlib.Min(Period, 0);
    internal IIndicator Create() => new DecayingWindowExtreme(Period, Operation == "Max");
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        if (gpu) builder.ConfigureExecution(IndicatorExecutionBackend.Gpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("Close-extreme benchmark did not dispatch GPU work.");
        }
        return run;
    }
}
