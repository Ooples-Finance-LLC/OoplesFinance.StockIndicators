using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;
using CandleTuple = (decimal Open, decimal High, decimal Low, decimal Close);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedContainmentGapBenchmarks
{
    [Params("Harami", "BullishHarami", "BearishHarami", "UpsideTasukiGap", "DownsideTasukiGap")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private const int Period = 3;
    private Bar[] _bars = null!;
    private CandleTuple[] _candles = null!;
    private CandleTuple[][] _segments = null!;
    private int[] _skips = null!;
    private int _priorDop;
    private bool IsTasuki => Operation.EndsWith("TasukiGap", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var fixture = IsTasuki ? TrendTasukiComparison.Fixture(Period).IndicatorBars
            : StrictHaramiComparison.Fixture(Period).IndicatorBars;
        _bars = Enumerable.Range(0, Count).Select(i => fixture[i % fixture.Length]).ToArray();
        _candles = _bars.Select(b => ((decimal)b.Open, (decimal)b.High, (decimal)b.Low, (decimal)b.Close)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _segments = new CandleTuple[chunks][];
        _skips = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            int begin = Math.Max(0, start - (Operation == "Harami" ? 1 : Period + 1));
            _skips[chunk] = start - begin;
            _segments[chunk] = _candles.AsSpan(begin, end - begin).ToArray();
        }
        var native = NativeSingle();
        if (!native.SequenceEqual(NativeParallel().SelectMany(s => s)))
            throw new InvalidOperationException("Native containment/gap partition mismatch.");
        var data = CompetitorData.FromOhlc(_bars.Select(b => b.Open).ToArray(), _bars.Select(b => b.High).ToArray(),
            _bars.Select(b => b.Low).ToArray(), _bars.Select(b => b.Close).ToArray());
        var pair = IsTasuki ? TrendTasukiComparison.CreatePair(Operation == "UpsideTasukiGap")
            : StrictHaramiComparison.Pair(Operation);
        var oracle = pair.Reference!(data, Period).Outputs["Value"];
        int absent = Operation == "Harami" ? 1 : IsTasuki ? 2 : 0;
        if (!native.Contains(true) || !native.Contains(false)
            || native.Where((v, i) => v.HasValue != (i >= absent)).Any())
            throw new InvalidOperationException("Unexpected native startup or degenerate containment/gap fixture.");
        AsinFeasibilityBenchmarks.RequireSame(oracle.Values, native.Select(v => v == true ? 1d : 0d).ToArray());
        // The directional ByTuple classes inherit plain Harami in Trady 3.2.8.
        // Confirm the generic directional APIs against the IOhlcv wrapper too.
        var wrapped = pair.Competitor(data, Period).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(wrapped, native.Select(v => v.HasValue ? (v.Value ? 1d : 0d) : double.NaN).ToArray());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            var indicator = Create();
            using var run = Build(indicator, history, reference);
            AsinFeasibilityBenchmarks.RequireSame(oracle.Values, run[indicator].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public IReadOnlyList<bool?> NativeSingle() => Native(_candles);
    [Benchmark] public IReadOnlyList<bool?>[] NativeParallel()
    {
        var output = new IReadOnlyList<bool?>[_skips.Length];
        CpuParallelSettings.LightweightParallel(output.Length, output.Length, chunk =>
            output[chunk] = Native(_segments[chunk], _skips[chunk]));
        return output;
    }
    private IReadOnlyList<bool?> Native(CandleTuple[] candles, int? start = null) => Operation switch
    {
        "Harami" => new TC.HaramiByTuple(candles).Compute(start, null),
        "BullishHarami" => new TC.BullishHarami<CandleTuple, bool?>(candles, static c => c, false, Period).Compute(start, null),
        "BearishHarami" => new TC.BearishHarami<CandleTuple, bool?>(candles, static c => c, false, Period).Compute(start, null),
        "UpsideTasukiGap" => new TC.UpsideTasukiGapByTuple(candles, Period).Compute(start, null),
        "DownsideTasukiGap" => new TC.DownsideTasukiGapByTuple(candles, Period).Compute(start, null),
        _ => throw new InvalidOperationException("Unknown containment/gap pattern.")
    };
    private IIndicator Create() => IsTasuki ? TrendTasukiComparison.Create(Operation == "UpsideTasukiGap", Period)
        : StrictHaramiComparison.Create(Operation, Period);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
