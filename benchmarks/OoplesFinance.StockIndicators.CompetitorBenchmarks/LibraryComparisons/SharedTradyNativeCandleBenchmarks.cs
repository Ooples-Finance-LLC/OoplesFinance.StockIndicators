using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Candlestick;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyNativeCandleBenchmarks : SharedMappedBenchmarkBase
{
    [Params("DarkCloudCover", "DragonifyDoji", "RisingThreeMethods", "FallingThreeMethods")]
    public string Operation { get; set; } = "";
    private (decimal Open, decimal High, decimal Low, decimal Close)[] _prices = null!;
    private IEnumerable<(decimal Open, decimal High, decimal Low, decimal Close)>[] _windows = null!;
    private int[] _ends = null!, _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("DarkCloudCover" or "DragonifyDoji" or "RisingThreeMethods" or "FallingThreeMethods"))
            throw new ArgumentException("Unknown Trady candle operation.");
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (pair, data) = PrepareFixture("Trady.Candlestick." + Operation);
        _prices = data.Candles.Select(c => (c.Open, c.High, c.Low, c.Close)).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Three-method patterns search arbitrarily far back for their anchor.
        // Dark cloud needs a 20-bar trend plus its 3-bar delay; doji is pointwise.
        _seeds = Enumerable.Range(0, workers).Select(i => Operation switch
        {
            "DragonifyDoji" => i == 0 ? 0 : _ends[i - 1],
            "DarkCloudCover" => Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 23),
            _ => 0
        }).ToArray();
        _windows = _ends.Select((end, i) => _prices.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var expected = pair.Reference!(data, 20);
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, ["Value"], includeScalarStartup: true);
        var column = expected.Outputs["Value"];
        var native = column.Values.Select((v, i) => i < column.FirstValid ? (bool?)null : v > 0).ToArray();
        if (!native.SequenceEqual(Normalize(NativeSingle())) || !native.SequenceEqual(Normalize(NativeParallel())))
            throw new InvalidOperationException("Native candle values or nulls differ from the independent reference.");
    }

    protected override IIndicator Create() => Operation switch
    {
        "DarkCloudCover" => new DelayedDarkCloudCoverPattern(20, 3),
        "DragonifyDoji" => new DragonflyDojiCandle(),
        "RisingThreeMethods" => new RisingThreeMethodsPattern(20, 20),
        _ => new FallingThreeMethodsPattern(20, 20)
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation == "DragonifyDoji"
        ? new DragonifyDojiByTuple(_prices, .1m, .1m).Compute()
        : ComputeNullable(_prices);

    [Benchmark]
    public object NativeParallel() => Operation == "DragonifyDoji"
        ? Parallel<bool>(static (prices, first, last) => new DragonifyDojiByTuple(prices, .1m, .1m).Compute(startIndex: first, endIndex: last))
        : Parallel<bool?>((prices, first, last) => ComputeNullable(prices, first, last));

    private T[] Parallel<T>(Func<IEnumerable<(decimal Open, decimal High, decimal Low, decimal Close)>, int, int, IReadOnlyList<T>> calculate)
    {
        var joined = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            var rows = (List<T>)calculate(_windows[worker], first - _seeds[worker], _ends[worker] - _seeds[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native candle range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    private List<bool?> ComputeNullable(IEnumerable<(decimal Open, decimal High, decimal Low, decimal Close)> prices,
        int? first = null, int? last = null) => (List<bool?>)(Operation switch
        {
            "DarkCloudCover" => new DarkCloudCoverByTuple(prices, 20, 3, 20, .75m).Compute(startIndex: first, endIndex: last),
            "RisingThreeMethods" => new RisingThreeMethodsByTuple(prices, 20, 20, .25m, .75m).Compute(startIndex: first, endIndex: last),
            _ => new FallingThreeMethodsByTuple(prices, 20, 20, .25m, .75m).Compute(startIndex: first, endIndex: last)
        });

    internal static bool?[] Normalize(object result) => result switch
    {
        IReadOnlyList<bool> rows => rows.Select(v => (bool?)v).ToArray(),
        IReadOnlyList<bool?> rows => rows.ToArray(),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };
}
