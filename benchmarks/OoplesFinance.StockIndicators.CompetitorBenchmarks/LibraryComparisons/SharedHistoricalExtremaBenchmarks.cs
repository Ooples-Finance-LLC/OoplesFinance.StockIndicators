using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedHistoricalExtremaBenchmarks
{
    [Params("HistoricalHighest", "HistoricalLowest", "HistoricalHighestClose", "HistoricalLowestClose", "HistoricalHighestHigh", "HistoricalLowestLow")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private decimal[] _input = null!;
    private decimal[][] _chunks = null!;
    private int _priorDop;
    private bool Maximum => Operation.Contains("Highest", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        bool candle = Operation is "HistoricalHighestHigh" or "HistoricalLowestLow";
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double close = (i * 13 % 101 - 50) / 8d;
            // Scalar/close mappings use close bars, as in the shared comparison manifest.
            return new Bar(default, close, candle ? close + (i % 7 + 1) / 8d : close,
                candle ? close - (i % 5 + 1) / 8d : close, close, 1);
        }).ToArray();
        var values = _bars.Select(b => Maximum ? b.High : b.Low).ToArray();
        _input = values.Select(v => (decimal)v).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new decimal[chunks][];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            _chunks[chunk] = _input[start..end];
        }
        var expected = new double[Count];
        double best = values[0];
        for (int i = 0; i < Count; i++)
        {
            if (Maximum ? values[i] > best : values[i] < best) best = values[i];
            expected[i] = best;
        }
        CheckNative(NativeSingle(), expected); CheckNative(NativeParallel(), expected);
        // Independent linear prefix oracle avoids a quadratic full-history evaluator in setup.
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
        SharedWindowBenchmarkValidation.Gpu(Create(), Build);
    }
    private static void CheckNative(IReadOnlyList<decimal?> values, double[] expected)
    {
        if (values.Count != expected.Length) throw new InvalidOperationException("Unexpected native extrema length.");
        for (int i = 0; i < values.Count; i++)
            if (!values[i].HasValue || BitConverter.DoubleToInt64Bits((double)values[i]!.Value) != BitConverter.DoubleToInt64Bits(expected[i]))
                throw new InvalidOperationException("Native extrema value or presence differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(Create(), IndicatorHistoryMode.Full, gpu: true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly, gpu: true);
    // The tuple API bypasses named candle wrappers while executing their same extrema operation.
    // Both native arms receive decimal conversion, field selection and chunk slicing before timing; prefix summaries/merges are timed.
    [Benchmark(Baseline = true)] public IReadOnlyList<decimal?> NativeSingle() => Native(_input);
    [Benchmark] public IReadOnlyList<decimal?> NativeParallel()
    {
        var output = new decimal?[Count];
        var locals = new IReadOnlyList<decimal?>[_chunks.Length];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk => locals[chunk] = Native(_chunks[chunk]));
        var carries = new decimal[_chunks.Length];
        decimal best = locals[0][^1]!.Value;
        for (int chunk = 1; chunk < locals.Length; chunk++)
        {
            carries[chunk] = best;
            decimal next = locals[chunk][^1]!.Value;
            if (Maximum ? next > best : next < best) best = next;
        }
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            int start = Count * chunk / _chunks.Length;
            var values = locals[chunk];
            for (int i = 0; i < values.Count; i++)
            {
                decimal next = values[i]!.Value;
                output[start + i] = chunk > 0 && (Maximum ? carries[chunk] > next : carries[chunk] < next) ? carries[chunk] : next;
            }
        });
        return output;
    }
    private IReadOnlyList<decimal?> Native(decimal[] values) => Maximum
        ? new HistoricalHighestByTuple(values).Compute()
        : new HistoricalLowestByTuple(values).Compute();
    private IIndicator Create() => Maximum ? new OoplesFinance.StockIndicators.Indicators.HighestHigh(Count)
        : new OoplesFinance.StockIndicators.Indicators.LowestLow(Count);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        return SharedBenchmarkSupport.BuildChecked(builder, gpu, "Historical extrema benchmark did not dispatch GPU work.");
    }
}
