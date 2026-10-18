using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;
using CandleTuple = (decimal Open, decimal High, decimal Low, decimal Close);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTrendReversalBenchmarks
{
    [Params("BullishAbandonedBaby", "BearishAbandonedBaby", "MorningStar", "EveningStar", "MorningDojiStar", "EveningDojiStar", "MoringinDojiStar")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private const int Trend = 3, Period = 20;
    private Bar[] _bars = null!;
    private CandleTuple[] _candles = null!;
    private CandleTuple[][] _segments = null!;
    private int[] _skips = null!;
    private int _priorDop;
    private bool IsBaby => Operation.EndsWith("AbandonedBaby", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var fixture = IsBaby ? TrendBabyComparison.Fixture(Trend).IndicatorBars
            : TrendStarComparison.Fixture(Trend).IndicatorBars;
        _bars = Enumerable.Range(0, Count).Select(i => fixture[i % fixture.Length]).ToArray();
        _candles = _bars.Select(b => ((decimal)b.Open, (decimal)b.High, (decimal)b.Low, (decimal)b.Close)).ToArray();
        // Native percentile components reconstruct a series per observation.
        // Small public-API blocks avoid repeatedly reconstructing long series.
        int chunks = (Count + Period - 1) / Period;
        _segments = new CandleTuple[chunks][];
        _skips = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Period * chunk, end = Math.Min(Count, start + Period);
            int begin = Math.Max(0, start - Math.Max(Trend + 1, Period + 1));
            _skips[chunk] = start - begin;
            _segments[chunk] = _candles.AsSpan(begin, end - begin).ToArray();
        }
        var native = NativeSingle().SelectMany(s => s).ToArray();
        if (!native.SequenceEqual(NativeParallel().SelectMany(s => s)))
            throw new InvalidOperationException("Native trend-reversal partition mismatch.");
        var data = CompetitorData.FromOhlc(_bars.Select(b => b.Open).ToArray(), _bars.Select(b => b.High).ToArray(),
            _bars.Select(b => b.Low).ToArray(), _bars.Select(b => b.Close).ToArray());
        var pair = IsBaby ? TrendBabyComparison.Pair(Operation == "BullishAbandonedBaby")
            : TrendStarComparison.Pair(Operation);
        var oracle = pair.Reference!(data, Trend).Outputs["Value"];
        int absent = 2;
        if (!native.Contains(true) || !native.Contains(false)
            || native.Where((v, i) => v.HasValue != (i >= absent)).Any())
            throw new InvalidOperationException("Unexpected native startup or degenerate reversal fixture.");
        AsinFeasibilityBenchmarks.RequireSame(oracle.Values, native.Select(v => v == true ? 1d : 0d).ToArray());
        // Check full-data decimal semantics separately from the library's exact
        // midpoint comparison, then verify an unpartitioned ordinary native prefix.
        var nativeOracle = (pair.CompetitorReference ?? pair.Reference)!(data, Trend).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(nativeOracle, native.Select(v => v == true ? 1d : 0d).ToArray());
        int prefix = Math.Min(256, Count);
        var prefixData = CompetitorData.FromOhlc(data.Opens[..prefix], data.Highs[..prefix], data.Lows[..prefix], data.Closes[..prefix]);
        var wrapped = pair.Competitor(prefixData, Trend).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(wrapped,
            native.Take(prefix).Select(v => v.HasValue ? (v.Value ? 1d : 0d) : double.NaN).ToArray());
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
    [Benchmark(Baseline = true)] public IReadOnlyList<bool?>[] NativeSingle()
    {
        var output = new IReadOnlyList<bool?>[_skips.Length];
        for (int block = 0; block < output.Length; block++)
            output[block] = Native(_segments[block], _skips[block]);
        return output;
    }
    [Benchmark] public IReadOnlyList<bool?>[] NativeParallel()
    {
        var output = new IReadOnlyList<bool?>[_skips.Length];
        int workers = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            for (int block = output.Length * worker / workers; block < output.Length * (worker + 1) / workers; block++)
                output[block] = Native(_segments[block], _skips[block]);
        });
        return output;
    }
    private IReadOnlyList<bool?> Native(CandleTuple[] candles, int? start = null) => Operation switch
    {
        "BullishAbandonedBaby" => new TC.BullishAbandonedBabyByTuple(candles, Trend).Compute(start, null),
        "BearishAbandonedBaby" => new TC.BearishAbandonedBabyByTuple(candles, Trend).Compute(start, null),
        "MorningStar" => new TC.MorningStarByTuple(candles, Trend).Compute(start, null),
        "EveningStar" => new TC.EveningStarByTuple(candles, Trend).Compute(start, null),
        "MorningDojiStar" => new TC.MorningDojiStar<CandleTuple, bool?>(candles, static c => c, Trend).Compute(start, null),
        "EveningDojiStar" => new TC.EveningDojiStarByTuple(candles, Trend).Compute(start, null),
        "MoringinDojiStar" => new TC.MoringinDojiStarByTuple(candles, Trend).Compute(start, null),
        _ => throw new InvalidOperationException("Unknown trend reversal.")
    };
    internal IIndicator Create() => IsBaby ? TrendBabyComparison.Create(Operation == "BullishAbandonedBaby", Trend)
        : TrendStarComparison.Create(Operation, Trend);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false,
        IndicatorExecutionBackend backend = IndicatorExecutionBackend.Auto)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history).ConfigureExecution(backend);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
