using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyMomentumBenchmarks
{
    private const int Period = 20;
    [Params("Momentum", "Difference", "UpMomentum", "PositiveDifference", "DownMomentum", "NegativeDifference")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private decimal?[] _input = null!;
    private decimal?[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    private PriceChangeKind _kind;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _kind = TradyMomentumComparison.Forms.Single(f => f.Name == Operation).Kind;
        var values = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _bars = values.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        _input = values.Select(v => (decimal?)(decimal)v).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new decimal?[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            int seed = Math.Max(0, start - Period);
            _skip[chunk] = start - seed; _chunks[chunk] = _input[seed..end];
        }
        var expected = new double[Count];
        for (int i = Period; i < Count; i++)
        {
            decimal difference = (decimal)values[i] - (decimal)values[i - Period];
            expected[i] = (double)(_kind == PriceChangeKind.Gain ? Math.Max(0, difference)
                : _kind == PriceChangeKind.Loss ? Math.Max(0, -difference) : difference);
        }
        CheckNative(NativeSingle(), expected); CheckNative(NativeParallel(), expected);
        SharedWindowBenchmarkValidation.Cpu(expected, Create, (i, h, r) => Build(i, h, r));
        SharedWindowBenchmarkValidation.Gpu(Create(), Build);
    }
    private static void CheckNative(IReadOnlyList<decimal?> values, double[] expected)
    {
        if (values.Count != expected.Length) throw new InvalidOperationException("Unexpected native momentum length.");
        for (int i = 0; i < values.Count; i++)
            if (i < Period ? values[i].HasValue : !values[i].HasValue || BitConverter.DoubleToInt64Bits((double)values[i]!.Value) != BitConverter.DoubleToInt64Bits(expected[i]))
                throw new InvalidOperationException("Native momentum value or presence differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(Create(), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly, gpu: true);
    // The tuple API bypasses named candle wrappers while executing their same lagged-change operation.
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
    private IReadOnlyList<decimal?> Native(decimal?[] values, int skip) => _kind switch
    {
        PriceChangeKind.Difference => new DifferenceByTuple(values, Period).Compute(startIndex: skip),
        PriceChangeKind.Gain => new PositiveDifferenceByTuple(values, Period).Compute(startIndex: skip),
        PriceChangeKind.Loss => new NegativeDifferenceByTuple(values, Period).Compute(startIndex: skip),
        _ => throw new InvalidOperationException("Unknown momentum mapping.")
    };
    private IIndicator Create() => new LaggedPriceChange(Period, _kind);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("Momentum benchmark did not dispatch GPU work.");
        }
        return run;
    }
}
