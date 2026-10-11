using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRollingPercentileBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Median", "QuanTAlib.Percentile", "Trady.Indicator.Median", "Trady.Indicator.Percentile")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _quanInput = null!;
    private decimal?[] _tradyInput = null!;
    private decimal?[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    private bool Quan => Operation.StartsWith("QuanTAlib.", StringComparison.Ordinal);
    private bool Median => Operation.EndsWith(".Median", StringComparison.Ordinal);
    private double Fraction => Median ? .5 : .25;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var values = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _bars = values.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        _quanInput = values.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        _tradyInput = values.Select(v => (decimal?)(decimal)v).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new decimal?[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks, seed = Math.Max(0, start - Period + 1);
            _skip[chunk] = start - seed; _chunks[chunk] = _tradyInput[seed..end];
        }
        // Independent exact oracle, including competitor-specific startup conventions.
        var data = CompetitorData.FromOhlc(values, values.Select(v => v + 1).ToArray(), values.Select(v => v - 1).ToArray(), values);
        var pair = PercentileComparison.Create(Quan, Median, Fraction);
        var expected = pair.Reference!(data, Period).Outputs["Value"].Values;
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"].Values;
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create(); using var run = Build(indicator, history);
            int start = Quan ? 0 : Period - 1;
            AsinFeasibilityBenchmarks.RequireSame(expected.AsSpan(start).ToArray(), run[indicator].Slice(start).ToArray());
        }
    }
    private void CheckNative(NativeResult result, double[] expected)
    {
        if (Quan) { AsinFeasibilityBenchmarks.RequireSame(expected, result.Doubles!); return; }
        if (result.Nullable!.Count != Count) throw new InvalidOperationException("Unexpected native percentile length.");
        for (int i = 0; i < Count; i++)
            if (i < Period - 1 ? result.Nullable[i].HasValue : !result.Nullable[i].HasValue
                || BitConverter.DoubleToInt64Bits((double)result.Nullable[i]!.Value) != BitConverter.DoubleToInt64Bits(expected[i]))
                throw new InvalidOperationException("Native percentile value or startup differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    public readonly record struct NativeResult(double[]? Doubles = null, IReadOnlyList<decimal?>? Nullable = null);
    [Benchmark(Baseline = true)] public NativeResult NativeSingle()
    {
        if (!Quan) return new(Nullable: Trady(_tradyInput, 0));
        var result = new double[Count]; CalculateQuan(0, Count, result); return new(Doubles: result);
    }
    [Benchmark] public NativeResult NativeParallel()
    {
        var result = Quan ? new NativeResult(Doubles: new double[Count]) : new NativeResult(Nullable: new decimal?[Count]);
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            int start = Count * chunk / _chunks.Length, end = Count * (chunk + 1) / _chunks.Length;
            if (Quan) CalculateQuan(start, end, result.Doubles!);
            else
            {
                var rows = Trady(_chunks[chunk], _skip[chunk]);
                var target = (decimal?[])result.Nullable!;
                for (int i = 0; i < rows.Count; i++) target[start + i] = rows[i];
            }
        });
        return result;
    }
    private void CalculateQuan(int start, int end, double[] result)
    {
        QuanTAlib.AbstractBase state = Median ? new QuanTAlib.Median(Period) : new QuanTAlib.Percentile(Period, Fraction * 100);
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_quanInput[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_quanInput[i]).Value;
    }
    private IReadOnlyList<decimal?> Trady(decimal?[] values, int skip) => Median
        ? new MedianByTuple(values, Period).Compute(startIndex: skip)
        : new PercentileByTuple(values, Period, (decimal)Fraction).Compute(startIndex: skip);
    internal RollingPercentile Create() => new(Period, Fraction, Quan);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool gpu = false) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
