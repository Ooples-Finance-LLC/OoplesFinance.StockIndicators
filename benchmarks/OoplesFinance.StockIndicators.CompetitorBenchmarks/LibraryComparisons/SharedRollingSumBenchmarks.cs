using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRollingSumBenchmarks
{
    private const int Period = 20;
    [Params(1_000, 10_000)] public int Count { get; set; }
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
        {
            var indicator = new RollingPriceSum(Period);
            using var run = Build(indicator, mode);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].Slice(Period - 1).ToArray());
            if (run[indicator].Slice(0, Period - 1).ToArray().Any(v => v != 0))
                throw new InvalidOperationException("Unexpected sum startup.");
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new RollingPriceSum(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new RollingPriceSum(Period), IndicatorHistoryMode.LatestOnly);
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
        var status = Functions.Sum<double>(input, System.Range.All, output, out var range, Period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period - 1
            || range.End.Value - range.Start.Value != output.Length) throw new InvalidOperationException("Native sum failed.");
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
