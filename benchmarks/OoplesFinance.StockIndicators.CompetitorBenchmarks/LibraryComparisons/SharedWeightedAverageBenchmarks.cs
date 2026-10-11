using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedWeightedAverageBenchmarks
{
    private const int Period = 20;
    [Params("TaLib.Functions.Wma")] public string Operation { get; set; } = null!;
    [Params(1_000, 10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!;
    private int _previousDop;
    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _closes = Enumerable.Range(0, Count).Select(i => 10 + (i % 101) / 8d).ToArray();
        _bars = _closes.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        var expected = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeParallel());
        foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        foreach (bool gpu in new[] { false, true })
        {
            var indicator = new Wma(Period);
            using var run = Build(indicator, mode, gpu);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].Slice(Period - 1).ToArray());
            var startup = new double[Period - 1];
            for (int i = 0; i < startup.Length; i++)
            {
                double weighted = 0;
                for (int j = 0; j <= i; j++) weighted += _closes[j] * (Period - i + j);
                startup[i] = weighted / (Period * (Period + 1) / 2d);
            }
            AsinFeasibilityBenchmarks.RequireSame(startup, run[indicator].Slice(0, Period - 1).ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new Wma(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new Wma(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(new Wma(Period), IndicatorHistoryMode.Full, true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(new Wma(Period), IndicatorHistoryMode.LatestOnly, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        Native(_closes, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        var chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
            Native(_closes.AsSpan(start, end - start + Period - 1), output.AsSpan(start, end - start));
        });
        return output;
    }
    private static void Native(ReadOnlySpan<double> input, Span<double> output)
    {
        var status = Functions.Wma<double>(input, System.Range.All, output, out var range, Period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period - 1
            || range.End.Value - range.Start.Value != output.Length) throw new InvalidOperationException("Native WMA failed.");
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(mode).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution!.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose(); throw new InvalidOperationException("Required GPU WMA did not execute on the device.");
        }
        return run;
    }
}
