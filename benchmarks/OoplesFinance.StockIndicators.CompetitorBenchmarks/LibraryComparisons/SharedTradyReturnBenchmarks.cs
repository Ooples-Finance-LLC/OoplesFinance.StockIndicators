using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyReturnBenchmarks
{
    private const int Period = 20;
    [Params("RateOfChange", "PercentageDifference")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private decimal?[] _input = null!;
    private decimal?[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var values = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _bars = values.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        _input = values.Select(v => (decimal?)(decimal)v).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new decimal?[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks, seed = Math.Max(0, start - Period);
            _skip[chunk] = start - seed; _chunks[chunk] = _input[seed..end];
        }
        var native = new decimal?[Count]; var owned = new double[Count]; var present = new double[Count];
        for (int i = Period; i < Count; i++)
        {
            if (_input[i - Period]!.Value == 0) continue;
            native[i] = (_input[i]!.Value - _input[i - Period]!.Value) / _input[i - Period]!.Value * 100;
            var previous = Units(values[i - Period]);
            owned[i] = Round((Units(values[i]) - previous) * 100, previous);
            present[i] = 1;
        }
        CheckNative(native, NativeSingle()); CheckNative(native, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new RateOfChangeWithValidity(Period); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(owned, run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(present, run[indicator.IsDefined].ToArray());
        }
    }
    private static void CheckNative(decimal?[] expected, IReadOnlyList<decimal?> actual)
    {
        if (actual.Count != expected.Length || !expected.SequenceEqual(actual))
            throw new InvalidOperationException("Native percentage output or presence differs from decimal reference.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new RateOfChangeWithValidity(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new RateOfChangeWithValidity(Period), IndicatorHistoryMode.LatestOnly);
    // RateOfChange delegates to PercentageDifference. Both mappings use its public tuple API,
    // excluding candle wrappers and decimal conversion from native timing.
    [Benchmark(Baseline = true)] public IReadOnlyList<decimal?> NativeSingle() => Native(_input, 0);
    [Benchmark] public IReadOnlyList<decimal?> NativeParallel()
    {
        var output = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            var rows = Native(_chunks[chunk], _skip[chunk]); int start = Count * chunk / _chunks.Length;
            for (int i = 0; i < rows.Count; i++) output[start + i] = rows[i];
        });
        return output;
    }
    private static IReadOnlyList<decimal?> Native(decimal?[] values, int skip) => new PercentageDifferenceByTuple(values, Period).Compute(startIndex: skip);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
