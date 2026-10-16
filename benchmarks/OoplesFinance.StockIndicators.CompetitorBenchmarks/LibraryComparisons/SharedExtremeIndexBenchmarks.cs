using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedExtremeIndexBenchmarks
{
    private const int Period = 20;
    [Params("MinIndex", "MaxIndex", "MinMaxIndex")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _close = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _close = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _bars = _close.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        var indicators = Create();
        var expected = indicators.Select(indicator => _close.Select((_, i) =>
        {
            int best = i;
            for (int j = i - 1; j >= Math.Max(0, i - Period + 1); j--)
                if (indicator.Maximum ? _close[j] > _close[best] : _close[j] < _close[best]) best = j;
            return (double)best;
        }).ToArray()).ToArray();
        var native = NativeSingle();
        var parallel = NativeParallel();
        for (int output = 0; output < expected.Length; output++)
        {
            AsinFeasibilityBenchmarks.RequireSame(expected[output].Skip(Period - 1).ToArray(), Values(native, output));
            AsinFeasibilityBenchmarks.RequireSame(Values(native, output), Values(parallel, output));
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            using var run = Build(indicators, history, reference);
            for (int output = 0; output < indicators.Length; output++)
                AsinFeasibilityBenchmarks.RequireSame(expected[output], run[indicators[output]].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    // Preserve the native API's int output for single extrema and two double arrays for MinMaxIndex.
    public readonly record struct NativeResult(int[]? Index, double[]? Min, double[]? Max);
    private NativeResult Allocate() => Operation == "MinMaxIndex"
        ? new(null, new double[Count - Period + 1], new double[Count - Period + 1])
        : new(new int[Count - Period + 1], null, null);
    [Benchmark(Baseline = true)] public NativeResult NativeSingle()
    {
        var result = Allocate();
        Native(Period - 1, Count, result);
        return result;
    }
    [Benchmark] public NativeResult NativeParallel()
    {
        var result = Allocate();
        int chunks = Math.Min(8, Environment.ProcessorCount), length = Count - Period + 1;
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            Native(Period - 1 + length * chunk / chunks, Period - 1 + length * (chunk + 1) / chunks, result));
        return result;
    }
    private void Native(int start, int end, NativeResult result)
    {
        // The full input span and disjoint output ranges preserve absolute native indices.
        // TA-Lib input ranges have inclusive ends; output ranges are end-exclusive.
        // It reads P-1 preceding values itself; no copied slices or postprocessing.
        int offset = start - Period + 1, length = end - start;
        System.Range range;
        var code = Operation switch
        {
            "MinIndex" => Functions.MinIndex<double>(_close, new System.Range(start, end - 1), result.Index.AsSpan(offset, length), out range, Period),
            "MaxIndex" => Functions.MaxIndex<double>(_close, new System.Range(start, end - 1), result.Index.AsSpan(offset, length), out range, Period),
            _ => Functions.MinMaxIndex<double>(_close, new System.Range(start, end - 1), result.Min.AsSpan(offset, length), result.Max.AsSpan(offset, length), out range, Period)
        };
        if (code != TALib.Core.RetCode.Success || range.Start.Value != start || range.End.Value != end)
            throw new InvalidOperationException("Unexpected native extreme-index output range.");
    }
    private static double[] Values(NativeResult result, int slot) => result.Index is not null
        ? result.Index.Select(i => (double)i).ToArray() : slot == 0 ? result.Min! : result.Max!;
    internal WindowExtremeIndex[] Create() => Operation == "MinMaxIndex"
        ? [new(Period), new(Period, true)] : [new(Period, Operation == "MaxIndex")];
    internal IIndicatorRun Build(WindowExtremeIndex[] indicators, IndicatorHistoryMode history, bool reference = false, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        if (gpu) builder.ConfigureExecution(IndicatorExecutionBackend.Gpu);
        return SharedBenchmarkSupport.BuildChecked(builder, gpu, "Index benchmark did not execute on the GPU.");
    }
}
