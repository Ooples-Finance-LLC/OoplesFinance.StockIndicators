using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Native returns packed full-window outputs. Builder additionally computes its
// available-history startup and Full retains owned bars. No adapter normalization
// is timed. Exact builder arithmetic and native floating moments differ in rounding.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedDispersionBenchmarks
{
    private const int Period = 20;
    [Params(false, true)] public bool Variance { get; set; }
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
        Check(expected, NativeParallel());
        foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var indicator = Create();
            using var result = Build(indicator, mode);
            Check(expected, result[indicator].Slice(Period - 1).ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        Native(_closes, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks;
            int end = output.Length * (chunk + 1) / chunks;
            // Each independent segment starts with its complete lookback window.
            Native(_closes.AsSpan(start, end - start + Period - 1), output.AsSpan(start, end - start));
        });
        return output;
    }
    private void Native(ReadOnlySpan<double> input, Span<double> output)
    {
        System.Range range;
        var status = Variance ? Functions.Var<double>(input, System.Range.All, output, out range, Period)
            : Functions.StdDev<double>(input, System.Range.All, output, out range, Period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period - 1
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native dispersion output range.");
    }
    private WindowDispersion Create() => new(Period,
        Variance ? WindowDispersionOutput.Variance : WindowDispersionOutput.StandardDeviation);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
    private static void Check(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(actual[i]) || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Dispersion mismatch at {i}.");
    }
}
