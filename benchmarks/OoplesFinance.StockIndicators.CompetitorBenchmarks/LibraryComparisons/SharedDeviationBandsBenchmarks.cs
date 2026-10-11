using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using BollingerBandsResult = Skender.Stock.Indicators.BollingerBandsResult;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedDeviationBandsBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetBollingerBands")] public string Operation { get; set; } = "";
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
        var values = Enumerable.Range(0, Count).Select(i => 10 + i % 101 / 8d).ToArray();
        _bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        _tuples = _bars.Select(b => (b.Time, b.Close)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _chunks = new (DateTime, double)[chunks][]; _skip = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks, seed = Math.Max(0, start - Period + 1);
            _skip[chunk] = start - seed; _chunks[chunk] = _tuples[seed..end];
        }
        var data = CompetitorData.FromCloses(values); var pair = DeviationBandsComparison.Pair(Operation);
        var expected = pair.Reference!(data, Period); var native = pair.CompetitorReference!(data, Period);
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new WindowDeviationBands(Period); using var run = Build(indicator, history);
            for (int slot = 0; slot < 6; slot++)
            {
                var column = expected.Outputs[DeviationBandsComparison.Names(Operation)[slot]];
                AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(column.Present!.Select(v => v ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 6]].ToArray());
            }
        }
    }
    private void CheckNative(IEnumerable<BollingerBandsResult> actual, ComparisonSeries expected)
    {
        var rows = actual.ToArray();
        for (int slot = 0; slot < 6; slot++)
        {
            var values = rows.Select(r => slot == 0 ? r.Sma : slot == 1 ? r.UpperBand : slot == 2 ? r.LowerBand : slot == 3 ? r.PercentB : slot == 4 ? r.ZScore : r.Width).ToArray();
            var column = expected.Outputs[DeviationBandsComparison.Names(Operation)[slot]];
            AsinFeasibilityBenchmarks.RequireSame(column.Present!.Select(v => v ? 1d : 0).ToArray(), values.Select(v => v.HasValue ? 1d : 0).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray(), values.Select(v => v ?? 0).ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowDeviationBands(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowDeviationBands(Period), IndicatorHistoryMode.LatestOnly);
    // Public double-tuple entry point excludes quote conversion; every native worker
    // computes its window from timed seeds and retains all six nullable outputs.
    [Benchmark(Baseline = true)] public IReadOnlyList<BollingerBandsResult> NativeSingle() => (IReadOnlyList<BollingerBandsResult>)_tuples.GetBollingerBands(Period);
    [Benchmark] public IReadOnlyList<BollingerBandsResult> NativeParallel()
    {
        var output = new BollingerBandsResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, chunk =>
        {
            int start = Count * chunk / _chunks.Length, i = -_skip[chunk];
            foreach (var row in _chunks[chunk].GetBollingerBands(Period))
            {
                if (i >= 0) output[start + i] = row;
                i++;
            }
        });
        return output;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
