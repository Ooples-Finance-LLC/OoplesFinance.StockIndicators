using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedDetailedDeviationBenchmarks
{
    private const int Period = 20, Smooth = 20;
    [Params("Skender.GetStdDev")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private (DateTime, double)[] _tuples = null!;
    private (DateTime, double)[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var input = SkenderWindowBenchmarkInput.Create(Count, Period + Smooth - 2);
        _bars = input.Bars; _tuples = input.Tuples; _chunks = input.Chunks; _skip = input.Skip;
        var data = CompetitorData.FromCloses(input.Prices); var pair = SkenderDeviationComparison.Create(Smooth);
        var expected = pair.Reference!(data, Period); var native = pair.CompetitorReference!(data, Period);
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new StandardDeviationWithDetails(Period, Smooth); using var run = Build(indicator, history);
            for (int slot = 0; slot < 4; slot++)
            {
                var column = expected.Outputs[SkenderDeviationComparison.Names[slot]];
                AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(column.Present!.Select(v => v ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 4]].ToArray());
            }
        }
    }
    private static void CheckNative(IEnumerable<StdDevResult> actual, ComparisonSeries expected)
    {
        var rows = actual.ToArray();
        for (int slot = 0; slot < 4; slot++)
        {
            var values = rows.Select(r => slot == 0 ? r.StdDev : slot == 1 ? r.Mean : slot == 2 ? r.ZScore : r.StdDevSma).ToArray();
            var column = expected.Outputs[SkenderDeviationComparison.Names[slot]];
            AsinFeasibilityBenchmarks.RequireSame(column.Present!.Select(v => v ? 1d : 0).ToArray(), values.Select(v => v.HasValue ? 1d : 0).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray(), values.Select(v => v ?? 0).ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new StandardDeviationWithDetails(Period, Smooth), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new StandardDeviationWithDetails(Period, Smooth), IndicatorHistoryMode.LatestOnly);
    // Public double-tuple entry point excludes quote conversion; every native worker
    // computes both windows from timed seeds and retains all four nullable outputs.
    [Benchmark(Baseline = true)] public IReadOnlyList<StdDevResult> NativeSingle() => (IReadOnlyList<StdDevResult>)_tuples.GetStdDev(Period, Smooth);
    [Benchmark] public IReadOnlyList<StdDevResult> NativeParallel()
    {
        var output = new StdDevResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            int start = Count * chunk / _chunks.Length, i = -_skip[chunk];
            foreach (var row in _chunks[chunk].GetStdDev(Period, Smooth))
            {
                if (i >= 0) output[start + i] = row;
                i++;
            }
        });
        return output;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
