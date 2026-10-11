using System.Numerics;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedNativeDispersionBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Variance", "QuanTAlib.Stddev", "QuanTAlib.Zscore", "Trady.Indicator.StandardDeviation")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _quan = null!;
    private decimal?[] _trady = null!;
    private decimal?[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    private bool Quan => Operation.StartsWith("QuanTAlib.", StringComparison.Ordinal);
    private WindowDispersionOutput Output => Operation.EndsWith("Variance", StringComparison.Ordinal) ? WindowDispersionOutput.Variance
        : Operation.EndsWith("Zscore", StringComparison.Ordinal) ? WindowDispersionOutput.ZScore : WindowDispersionOutput.StandardDeviation;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var units = Enumerable.Range(0, Count).Select(i => 80 + i % 101).ToArray();
        var values = units.Select(i => i / 8d).ToArray();
        _bars = values.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        _quan = values.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        _trady = values.Select(v => (decimal?)(decimal)v).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new decimal?[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks, seed = Math.Max(0, start - Period + 1);
            _skip[chunk] = start - seed; _chunks[chunk] = _trady[seed..end];
        }
        // Independent exact moments on the fixture's eighth-unit lattice.
        var expected = new double[Count];
        for (int i = 0; i < Count; i++)
        {
            int n = Math.Min(i + 1, Period); if (n < 2) continue;
            BigInteger sum = 0, squares = 0;
            for (int j = i - n + 1; j <= i; j++) { sum += units[j]; squares += units[j] * units[j]; }
            var variance = n * squares - sum * sum;
            int divisor = Quan ? n - 1 : n;
            if (Output == WindowDispersionOutput.Variance) expected[i] = Round(variance, n * divisor * 64);
            else if (Output == WindowDispersionOutput.StandardDeviation) expected[i] = DispersionReferenceArithmetic.Sqrt(variance, n * divisor * 64);
            else if (!variance.IsZero)
            {
                var deviation = n * units[i] - sum;
                expected[i] = deviation.Sign * DispersionReferenceArithmetic.Sqrt(deviation * deviation * divisor, n * variance);
            }
        }
        CheckNative(NativeSingle(), expected); CheckNative(NativeParallel(), expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create(); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }
    public readonly record struct NativeResult(double[]? Values = null, IReadOnlyList<decimal?>? Nullable = null);
    private void CheckNative(NativeResult actual, double[] expected)
    {
        var values = actual.Values ?? actual.Nullable!.Select(v => (double)(v ?? 0)).ToArray();
        if (values.Length != Count) throw new InvalidOperationException("Unexpected dispersion length.");
        if (!Quan && actual.Nullable!.Where((v, i) => v.HasValue != (i >= Period - 1)).Any())
            throw new InvalidOperationException("Unexpected native dispersion presence.");
        // Native centered floating/decimal moments have different rounding boundaries.
        for (int i = Quan ? 0 : Period - 1; i < Count; i++)
            if (!double.IsFinite(values[i]) || Math.Abs(values[i] - expected[i]) > 1e-12 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException("Native dispersion differs from independent exact reference.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public NativeResult NativeSingle()
    {
        if (!Quan) return new(Nullable: new StandardDeviationByTuple(_trady, Period).Compute());
        var result = new double[Count]; CalculateQuan(0, Count, result); return new(Values: result);
    }
    [Benchmark] public NativeResult NativeParallel()
    {
        var result = Quan ? new NativeResult(Values: new double[Count]) : new NativeResult(Nullable: new decimal?[Count]);
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            int start = Count * chunk / _chunks.Length, end = Count * (chunk + 1) / _chunks.Length;
            if (Quan) CalculateQuan(start, end, result.Values!);
            else
            {
                var rows = new StandardDeviationByTuple(_chunks[chunk], Period).Compute(startIndex: _skip[chunk]);
                var target = (decimal?[])result.Nullable!;
                for (int i = 0; i < rows.Count; i++) target[start + i] = rows[i];
            }
        });
        return result;
    }
    private void CalculateQuan(int start, int end, double[] result)
    {
        QuanTAlib.AbstractBase state = Output switch
        {
            WindowDispersionOutput.Variance => new QuanTAlib.Variance(Period),
            WindowDispersionOutput.StandardDeviation => new QuanTAlib.Stddev(Period),
            _ => new QuanTAlib.Zscore(Period)
        };
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_quan[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_quan[i]).Value;
    }
    private WindowDispersion Create() => new(Period, Output, Quan);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
