using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyExtremaBenchmarks
{
    private const int Period = 20;
    [Params("Highest", "Lowest", "HighestClose", "LowestClose", "HighestHigh", "LowestLow")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private decimal?[] _input = null!;
    private decimal?[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    private bool Maximum => Operation.StartsWith("Highest", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        bool candle = Operation is "HighestHigh" or "LowestLow";
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double close = (i * 13 % 101 - 50) / 8d;
            // Scalar/close mappings use close bars, as in the shared comparison manifest.
            return new Bar(default, close, candle ? close + (i % 7 + 1) / 8d : close,
                candle ? close - (i % 5 + 1) / 8d : close, close, 1);
        }).ToArray();
        var values = _bars.Select(b => Maximum ? b.High : b.Low).ToArray();
        _input = values.Select(v => (decimal?)(decimal)v).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new decimal?[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            int seed = Math.Max(0, start - Period + 1);
            _skip[chunk] = start - seed; _chunks[chunk] = _input[seed..end];
        }
        var expected = values.Select((_, i) =>
        {
            var window = values.Skip(Math.Max(0, i - Period + 1)).Take(Math.Min(i + 1, Period));
            return Maximum ? window.Max() : window.Min();
        }).ToArray();
        CheckNative(NativeSingle(), expected); CheckNative(NativeParallel(), expected);
        SharedWindowBenchmarkValidation.Cpu(expected, Create, (i, h, r) => Build(i, h, r));
        SharedWindowBenchmarkValidation.Gpu(Create(), Build);
    }
    private static void CheckNative(IReadOnlyList<decimal?> values, double[] expected)
    {
        if (values.Count != expected.Length) throw new InvalidOperationException("Unexpected native extrema length.");
        for (int i = 0; i < values.Count; i++)
            if (i < Period - 1 ? values[i].HasValue : !values[i].HasValue || BitConverter.DoubleToInt64Bits((double)values[i]!.Value) != BitConverter.DoubleToInt64Bits(expected[i]))
                throw new InvalidOperationException("Native extrema value or presence differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(Create(), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly, gpu: true);
    // The tuple API bypasses named candle wrappers while executing their same extrema operation.
    // Both native arms receive decimal conversion, field selection and chunk slicing before timing.
    [Benchmark(Baseline = true)] public IReadOnlyList<decimal?> NativeSingle() => Native(_input, 0);
    [Benchmark] public IReadOnlyList<decimal?> NativeParallel()
    {
        var output = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            int start = Count * chunk / _chunks.Length;
            var values = Native(_chunks[chunk], _skip[chunk]);
            for (int i = 0; i < values.Count; i++) output[start + i] = values[i];
        });
        return output;
    }
    private IReadOnlyList<decimal?> Native(decimal?[] values, int skip) => Maximum
        ? new HighestByTuple(values, Period).Compute(startIndex: skip)
        : new LowestByTuple(values, Period).Compute(startIndex: skip);
    private IIndicator Create() => Maximum ? new OoplesFinance.StockIndicators.Indicators.HighestHigh(Period)
        : new OoplesFinance.StockIndicators.Indicators.LowestLow(Period);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("Extrema benchmark did not dispatch GPU work.");
        }
        return run;
    }
}
