using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TC = Trady.Analysis.Candlestick;
using BodyTuple = (decimal Open, decimal Close);
using ShadowTuple = (decimal Open, decimal Extreme, decimal Close);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPercentileCandleBenchmarks
{
    [Params("LongDay", "ShortDay", "BullishLongDay", "BearishLongDay", "BullishShortDay", "BearishShortDay", "LongUpperShadow", "LongLowerShadow")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    // Three directional Trady wrappers ignore their arguments and use these defaults.
    private const int Period = 20;
    private Bar[] _bars = null!;
    private BodyTuple[] _bodies = null!;
    private ShadowTuple[] _shadows = null!;
    private BodyTuple[][] _bodySegments = null!;
    private ShadowTuple[][] _shadowSegments = null!;
    private int[] _skips = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var fixture = PercentileCandleComparison.Fixture().IndicatorBars;
        if (Operation is "BullishShortDay" or "BearishShortDay")
            fixture = [.. fixture, new Bar(default, 20, 22, 18, 20.125, 1), new Bar(default, 20, 22, 18, 19.875, 1)];
        _bars = Enumerable.Range(0, Count).Select(i => fixture[i % fixture.Length]).ToArray();
        _bodies = _bars.Select(b => ((decimal)b.Open, (decimal)b.Close)).ToArray();
        _shadows = _bars.Select(b => ((decimal)b.Open,
            (decimal)(Operation == "LongLowerShadow" ? b.Low : b.High), (decimal)b.Close)).ToArray();
        // Small public-API blocks avoid recomputing a long percentile series per candle.
        int chunks = (Count + Period - 1) / Period;
        _bodySegments = new BodyTuple[chunks][];
        _shadowSegments = new ShadowTuple[chunks][];
        _skips = new int[chunks];
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = Period * chunk, end = Math.Min(Count, start + Period);
            int begin = Math.Max(0, start - Period + 1);
            _skips[chunk] = start - begin;
            _bodySegments[chunk] = _bodies.AsSpan(begin, end - begin).ToArray();
            _shadowSegments[chunk] = _shadows.AsSpan(begin, end - begin).ToArray();
        }
        var native = NativeSingle().SelectMany(BooleanValues).ToArray();
        // Independently compare blocked execution against an unpartitioned public call.
        int prefix = Math.Min(256, Count);
        if (!native.Take(prefix).SequenceEqual(BooleanValues(Native(_bodies[..prefix], _shadows[..prefix]))))
            throw new InvalidOperationException("Native percentile block boundary mismatch.");
        if (!native.SequenceEqual(NativeParallel().SelectMany(BooleanValues)))
            throw new InvalidOperationException("Native percentile partition mismatch.");
        var data = CompetitorData.FromOhlc(_bars.Select(b => b.Open).ToArray(), _bars.Select(b => b.High).ToArray(),
            _bars.Select(b => b.Low).ToArray(), _bars.Select(b => b.Close).ToArray());
        var oracle = PercentileCandleComparison.Pairs.Single(p => p.Id == "Trady.Candlestick." + Operation)
            .Reference!(data, Period).Outputs["Value"].Values;
        // Package 3.2.8 returns false (including shadow nullable results) during startup.
        if (native.Any(v => !v.HasValue) || !native.Contains(true) || !native.Contains(false))
            throw new InvalidOperationException("Unexpected nullable results or degenerate percentile fixture.");
        AsinFeasibilityBenchmarks.RequireSame(oracle, native.Select(v => v == true ? 1d : 0d).ToArray());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            var indicator = Create();
            using var run = Build(indicator, history, reference);
            AsinFeasibilityBenchmarks.RequireSame(oracle, run[indicator].ToArray());
        }
    }
    private static IEnumerable<bool?> BooleanValues(object values) => values switch
    {
        IReadOnlyList<bool> body => body.Select(v => (bool?)v),
        IReadOnlyList<bool?> shadow => shadow,
        _ => throw new InvalidOperationException("Unexpected native result type.")
    };
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public object[] NativeSingle()
    {
        var output = new object[_skips.Length];
        for (int block = 0; block < output.Length; block++)
            output[block] = Native(_bodySegments[block], _shadowSegments[block], _skips[block]);
        return output;
    }
    [Benchmark] public object[] NativeParallel()
    {
        var output = new object[_skips.Length];
        int workers = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            for (int block = output.Length * worker / workers; block < output.Length * (worker + 1) / workers; block++)
                output[block] = Native(_bodySegments[block], _shadowSegments[block], _skips[block]);
        });
        return output;
    }
    // Return raw native outputs; public start selection excludes unused seed outputs.
    private object Native(BodyTuple[] body, ShadowTuple[] shadow, int? start = null) => Operation switch
    {
        "LongDay" => new TC.LongDayByTuple(body, Period).Compute(start, null),
        "ShortDay" => new TC.ShortDayByTuple(body, Period).Compute(start, null),
        "BullishLongDay" => new TC.BullishLongDayByTuple(body, Period).Compute(start, null),
        "BearishLongDay" => new TC.BearishLongDayByTuple(body, Period).Compute(start, null),
        "BullishShortDay" => new TC.BullishShortDayByTuple(body, Period).Compute(start, null),
        "BearishShortDay" => new TC.BearishShortDayByTuple(body, Period).Compute(start, null),
        "LongUpperShadow" => new TC.LongUpperShadowByTuple(shadow, Period).Compute(start, null),
        "LongLowerShadow" => new TC.LongLowerShadowByTuple(shadow, Period).Compute(start, null),
        _ => throw new InvalidOperationException("Unknown percentile candle.")
    };
    private IIndicator Create() => PercentileCandleComparison.Create(Operation, Period);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
