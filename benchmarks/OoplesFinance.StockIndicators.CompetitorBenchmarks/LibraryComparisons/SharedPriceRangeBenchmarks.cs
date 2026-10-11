using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;
using Prices = (decimal High, decimal Low);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPriceRangeBenchmarks
{
    private const int Period = 20;
    [Params("Trady.Indicator.HighestHighLowestLowDifference")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private Prices[] _input = null!;
    private Prices[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i => new Bar(default, 0, (i % 31 + 1) / 8d, -(i % 23 + 1) / 8d, 0, 1)).ToArray();
        _input = _bars.Select(b => ((decimal)b.High, (decimal)b.Low)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new Prices[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            int seed = Math.Max(0, start - Period + 1);
            _skip[chunk] = start - seed; _chunks[chunk] = _input[seed..end];
        }
        var expected = _bars.Select((_, i) =>
        {
            var window = _bars.Skip(Math.Max(0, i - Period + 1)).Take(Math.Min(i + 1, Period));
            return window.Max(b => b.High) - window.Min(b => b.Low);
        }).ToArray();
        CheckNative(NativeSingle(), expected); CheckNative(NativeParallel(), expected);
        SharedWindowBenchmarkValidation.Cpu(expected, () => new WindowPriceRange(Period), (i, h, r) => Build(i, h, r));
        SharedWindowBenchmarkValidation.Gpu(new WindowPriceRange(Period), Build);
    }
    private static void CheckNative(IReadOnlyList<decimal?> values, double[] expected)
    {
        if (values.Count != expected.Length) throw new InvalidOperationException("Unexpected native range length.");
        for (int i = 0; i < values.Count; i++)
            if (i < Period - 1 ? values[i].HasValue : !values[i].HasValue || BitConverter.DoubleToInt64Bits((double)values[i]!.Value) != BitConverter.DoubleToInt64Bits(expected[i]))
                throw new InvalidOperationException("Native range value or presence differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowPriceRange(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowPriceRange(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(new WindowPriceRange(Period), IndicatorHistoryMode.Full, reference: true);
    [Benchmark] public IIndicatorRun GpuFull() => Build(new WindowPriceRange(Period), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(new WindowPriceRange(Period), IndicatorHistoryMode.LatestOnly, gpu: true);
    [Benchmark(Baseline = true)] public IReadOnlyList<decimal?> NativeSingle() => new HighestHighLowestLowDifferenceByTuple(_input, Period).Compute();
    [Benchmark] public IReadOnlyList<decimal?> NativeParallel()
    {
        var output = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            int start = Count * chunk / _chunks.Length;
            var values = new HighestHighLowestLowDifferenceByTuple(_chunks[chunk], Period).Compute(startIndex: _skip[chunk]);
            for (int i = 0; i < values.Count; i++) output[start + i] = values[i];
        });
        return output;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("Price-range benchmark did not dispatch GPU work.");
        }
        return run;
    }
}
