using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;
using CandleTuple = (decimal Open, decimal High, decimal Low, decimal Close);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedShadowDojiBenchmarks
{
    [Params("DragonflyDoji", "GravestoneDoji")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private CandleTuple[] _tuples = null!;
    private CandleTuple[][] _segments = null!;
    private int _previousDop;
    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double low = 10 + (i % 37) / 8d, high = low + 10;
            double open = i % 4 == 0 ? high : i % 4 == 1 ? low : low + 5;
            return new Bar(default, open, high, low, open + (i % 7 == 0 ? 2 : (i % 5 - 2) / 8d), 1);
        }).ToArray();
        _tuples = _bars.Select(b => ((decimal)b.Open, (decimal)b.High, (decimal)b.Low, (decimal)b.Close)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _segments = Enumerable.Range(0, chunks).Select(c => _tuples.AsSpan(Count * c / chunks, Count * (c + 1) / chunks - Count * c / chunks).ToArray()).ToArray();
        var native = NativeSingle().ToArray();
        if (!native.SequenceEqual(NativeParallel().SelectMany(x => x))) throw new InvalidOperationException("Native partition mismatch.");
        var indicator = Create();
        // Every price is an exactly representable eighth and range is ten;
        // these bounded decimal predicates are exact on the prepared fixture.
        var oracle = _tuples.Select(t => Math.Abs(t.Close - t.Open) < (t.High - t.Low) / 10m
            && (Operation == "DragonflyDoji" ? t.High - (t.Open + t.Close) / 2m : (t.Open + t.Close) / 2m - t.Low)
                < (t.High - t.Low) / 10m ? 1d : 0d).ToArray();
        AsinFeasibilityBenchmarks.RequireSame(oracle, native.Select(v => v ? 1d : 0d).ToArray());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var result = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(oracle, result[indicator].ToArray());
        }
        using var ordinary = Build(indicator, IndicatorHistoryMode.Full, true);
        AsinFeasibilityBenchmarks.RequireSame(oracle, ordinary[indicator].ToArray());
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    // Use the lean tuple/bool API directly, without timestamped tick reflection
    // or double-array normalization. Fresh instances avoid cached answers.
    [Benchmark(Baseline = true)] public IReadOnlyList<bool> NativeSingle() => Native(_tuples);
    [Benchmark] public IReadOnlyList<bool>[] NativeParallel()
    {
        var output = new IReadOnlyList<bool>[_segments.Length];
        CpuParallelSettings.LightweightParallel(output.Length, output.Length, chunk => output[chunk] = Native(_segments[chunk]));
        return output;
    }
    private IReadOnlyList<bool> Native(CandleTuple[] source) => Operation == "DragonflyDoji"
        ? new TC.DragonifyDojiByTuple(source).Compute(null, null)
        : new TC.GravestoneDojiByTuple(source).Compute(null, null);
    private IIndicator Create() => Operation == "DragonflyDoji" ? new DragonflyDojiCandle() : new GravestoneDojiCandle();
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
