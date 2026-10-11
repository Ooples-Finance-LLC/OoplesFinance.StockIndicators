using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPriorChannelBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetDonchian")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private List<Quote> _quotes = null!;
    private List<Quote>[] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var close = Enumerable.Range(0, Count).Select(i => 10 + i % 101 / 8d).ToArray();
        var data = CompetitorData.FromOhlc(close, close.Select(v => v + 1).ToArray(), close.Select(v => v - 1).ToArray(), close);
        _quotes = data.Quotes; _bars = data.IndicatorBars;
        int workers = Math.Min(8, Environment.ProcessorCount);
        _chunks = new List<Quote>[workers]; _skip = new int[workers];
        for (int worker = 0; worker < workers; worker++)
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            int seed = Math.Max(0, start - Period);
            _skip[worker] = start - seed; _chunks[worker] = _quotes.GetRange(seed, end - seed);
        }
        var pair = PriceWindowChannelComparison.Pairs[0];
        var native = pair.CompetitorReference!(data, Period); var exact = pair.Reference!(data, Period);
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new PriorPriceChannel(Period); using var run = Build(indicator, history);
            for (int slot = 0; slot < 4; slot++)
            {
                var column = exact.Outputs[PriceWindowChannelComparison.ChannelNames[slot]];
                AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(column.Present!.Select(v => v ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 4]].ToArray());
            }
        }
    }
    private static void CheckNative(IReadOnlyList<DonchianResult> rows, ComparisonSeries expected)
    {
        for (int slot = 0; slot < 4; slot++)
        {
            var values = rows.Select(r => (double?)(slot == 0 ? r.UpperBand : slot == 1 ? r.LowerBand : slot == 2 ? r.Centerline : r.Width)).ToArray();
            var column = expected.Outputs[PriceWindowChannelComparison.ChannelNames[slot]];
            AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray(), values.Select(v => v ?? 0).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(column.Present!.Select(v => v ? 1d : 0).ToArray(), values.Select(v => v.HasValue ? 1d : 0).ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new PriorPriceChannel(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new PriorPriceChannel(Period), IndicatorHistoryMode.LatestOnly);
    // The public API accepts quotes only. Retain its eager native collection;
    // sorting/materialization within that API and P-price worker seeds are timed.
    [Benchmark(Baseline = true)] public IReadOnlyList<DonchianResult> NativeSingle() => (IReadOnlyList<DonchianResult>)_quotes.GetDonchian(Period);
    [Benchmark] public IReadOnlyList<DonchianResult> NativeParallel()
    {
        var output = new DonchianResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, worker =>
        {
            var local = (IReadOnlyList<DonchianResult>)_chunks[worker].GetDonchian(Period);
            int start = Count * worker / _chunks.Length;
            for (int i = _skip[worker]; i < local.Count; i++) output[start + i - _skip[worker]] = local[i];
        });
        return output;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
