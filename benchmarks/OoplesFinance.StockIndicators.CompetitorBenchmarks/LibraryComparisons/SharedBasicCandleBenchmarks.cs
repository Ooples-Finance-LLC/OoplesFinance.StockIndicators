using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;
using CandleTuple = (decimal Open, decimal High, decimal Low, decimal Close);
using BodyTuple = (decimal Open, decimal Close);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedBasicCandleBenchmarks
{
    [Params("Doji", "Bullish", "Bearish")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private CandleTuple[] _candles = null!;
    private BodyTuple[] _bodies = null!;
    private CandleTuple[][] _candleSegments = null!;
    private BodyTuple[][] _bodySegments = null!;
    private int _previousDop;
    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double low = 10 + (i % 37) / 8d, open = low + 5;
            return new Bar(default, open, low + 10, low, open + (i % 7 - 3) / 2d, 1);
        }).ToArray();
        _candles = _bars.Select(b => ((decimal)b.Open, (decimal)b.High, (decimal)b.Low, (decimal)b.Close)).ToArray();
        _bodies = _bars.Select(b => ((decimal)b.Open, (decimal)b.Close)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _candleSegments = Enumerable.Range(0, chunks).Select(c => _candles.AsSpan(Count * c / chunks, Count * (c + 1) / chunks - Count * c / chunks).ToArray()).ToArray();
        _bodySegments = Enumerable.Range(0, chunks).Select(c => _bodies.AsSpan(Count * c / chunks, Count * (c + 1) / chunks - Count * c / chunks).ToArray()).ToArray();
        var native = NativeSingle().ToArray();
        if (!native.SequenceEqual(NativeParallel().SelectMany(x => x))) throw new InvalidOperationException("Native candle partition mismatch.");
        var oracle = _candles.Select(t => (Operation switch
        {
            "Bullish" => t.Close > t.Open,
            "Bearish" => t.Close < t.Open,
            "Doji" => Math.Abs(t.Close - t.Open) < (t.High - t.Low) / 10m,
            _ => throw new InvalidOperationException("Unknown candle operation.")
        }) ? 1d : 0d).ToArray();
        AsinFeasibilityBenchmarks.RequireSame(oracle, native.Select(v => v ? 1d : 0d).ToArray());
        var indicator = Create();
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
    // Fresh instances and native bool lists: no timestamped tick reflection,
    // normalized double arrays, or reusable cached answers in the timed methods.
    [Benchmark(Baseline = true)] public IReadOnlyList<bool> NativeSingle() => Native(_candles, _bodies);
    [Benchmark] public IReadOnlyList<bool>[] NativeParallel()
    {
        var output = new IReadOnlyList<bool>[_bodySegments.Length];
        CpuParallelSettings.LightweightParallel(output.Length, output.Length,
            chunk => output[chunk] = Native(_candleSegments[chunk], _bodySegments[chunk]));
        return output;
    }
    private IReadOnlyList<bool> Native(CandleTuple[] candles, BodyTuple[] bodies) => Operation switch
    {
        "Bullish" => new TC.BullishByTuple(bodies).Compute(null, null),
        "Bearish" => new TC.BearishByTuple(bodies).Compute(null, null),
        "Doji" => new TC.DojiByTuple(candles).Compute(null, null),
        _ => throw new InvalidOperationException("Unknown candle operation.")
    };
    internal IIndicator Create() => Operation switch
    {
        "Bullish" => new BullishCandle(), "Bearish" => new BearishCandle(), "Doji" => new DojiCandle(),
        _ => throw new InvalidOperationException("Unknown candle operation.")
    };
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false,
        IndicatorExecutionBackend backend = IndicatorExecutionBackend.Cpu)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(backend);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
