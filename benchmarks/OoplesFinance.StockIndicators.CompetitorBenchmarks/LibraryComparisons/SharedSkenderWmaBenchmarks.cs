using System.Numerics;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderWmaBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetWma")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private (DateTime, double)[] _tuples = null!;
    private (DateTime, double)[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var input = SkenderWindowBenchmarkInput.Create(Count, Period - 1);
        _bars = input.Bars; _tuples = input.Tuples; _chunks = input.Chunks; _skip = input.Skip;
        var (exact, native) = References(input.Prices);
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new Wma(Period); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(exact, run[indicator].ToArray());
        }
    }
    private static (double[] Exact, double[] Native) References(double[] prices)
    {
        var exact = new double[prices.Length]; var native = new double[prices.Length];
        var units = prices.Select(Units).ToArray();
        const int divisor = Period * (Period + 1) / 2;
        for (int i = 0; i < prices.Length; i++)
        {
            BigInteger weighted = 0;
            double publishedSum = 0;
            for (int j = Math.Max(0, i - Period + 1); j <= i; j++)
            {
                int weight = Period - i + j;
                weighted += units[j] * weight;
                // Integer-grid oracle explicitly rounds each native multiplication,
                // term division and addition, independently of floating arithmetic.
                if (i >= Period - 1)
                    publishedSum = Add(publishedSum, Divide(Multiply(prices[j], weight), divisor));
            }
            exact[i] = Round(weighted, divisor * Grid);
            native[i] = publishedSum;
        }
        return (exact, native);
    }
    private static void CheckNative(IReadOnlyList<WmaResult> rows, double[] expected)
    {
        if (rows.Take(Period - 1).Any(r => r.Wma.HasValue) || rows.Skip(Period - 1).Any(r => !r.Wma.HasValue))
            throw new InvalidOperationException("Unexpected WMA presence.");
        AsinFeasibilityBenchmarks.RequireSame(expected.Skip(Period - 1).ToArray(), rows.Skip(Period - 1).Select(r => r.Wma!.Value).ToArray());
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new Wma(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new Wma(Period), IndicatorHistoryMode.LatestOnly);
    // Use native tuples directly; internal sorting, state/results and worker
    // seed calculations stay timed. Return native objects without mapping.
    [Benchmark(Baseline = true)] public IReadOnlyList<WmaResult> NativeSingle() => (IReadOnlyList<WmaResult>)_tuples.GetWma(Period);
    [Benchmark] public IReadOnlyList<WmaResult> NativeParallel()
    {
        var output = new WmaResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, worker =>
        {
            var local = (IReadOnlyList<WmaResult>)_chunks[worker].GetWma(Period);
            int start = Count * worker / _chunks.Length;
            for (int i = _skip[worker]; i < local.Count; i++) output[start + i - _skip[worker]] = local[i];
        });
        return output;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
