using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyNativeVolumeBenchmarks : SharedMappedBenchmarkBase
{
    [Params("PositiveVolumeIndex", "NegativeVolumeIndex", "AccumulationDistributionLine")]
    public string Operation { get; set; } = "";
    private (decimal Close, decimal Volume)[] _cv = null!;
    private (decimal High, decimal Low, decimal Close, decimal Volume)[] _hlcv = null!;
    private IEnumerable<(decimal Close, decimal Volume)>[] _cvPrefixes = null!;
    private IEnumerable<(decimal High, decimal Low, decimal Close, decimal Volume)>[] _hlcvPrefixes = null!;
    private int[] _ends = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("PositiveVolumeIndex" or "NegativeVolumeIndex" or "AccumulationDistributionLine"))
            throw new ArgumentException("Unknown Trady volume operation.");
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (pair, data) = PrepareFixture("Trady.Indicator." + Operation);
        _cv = data.Candles.Select(c => (c.Close, c.Volume)).ToArray();
        _hlcv = data.Candles.Select(c => (c.High, c.Low, c.Close, c.Volume)).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Full prefixes preserve the accumulated decimal state and first-bar seed.
        _cvPrefixes = _ends.Select(end => _cv.Take(end)).ToArray();
        _hlcvPrefixes = _ends.Select(end => _hlcv.Take(end)).ToArray();
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, 20), ["Value"]);
        var expected = Operation == "AccumulationDistributionLine"
            ? AccumulationDistributionComparison.TradyDecimalReference(data)
            : VolumeRecurrenceComparison.TradyReference(data, Operation == "PositiveVolumeIndex");
        if (!expected.SequenceEqual(NativeSingle()) || !expected.SequenceEqual(NativeParallel()))
            throw new InvalidOperationException("Native volume decimal values or presence differ from the independent reference.");
    }

    protected override IIndicator Create() => Operation == "AccumulationDistributionLine"
        ? new PriceAdjustedAccumulationDistribution()
        : new VolumeConditionedIndex(Operation == "PositiveVolumeIndex");

    [Benchmark(Baseline = true)]
    public List<decimal?> NativeSingle() => Compute(_cv, _hlcv);

    [Benchmark]
    public decimal?[] NativeParallel()
    {
        var joined = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            var rows = Compute(_cvPrefixes[worker], _hlcvPrefixes[worker], first, _ends[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native volume range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    private List<decimal?> Compute(IEnumerable<(decimal Close, decimal Volume)> cv,
        IEnumerable<(decimal High, decimal Low, decimal Close, decimal Volume)> hlcv, int? first = null, int? last = null) =>
        (List<decimal?>)(Operation switch
        {
            "PositiveVolumeIndex" => new PositiveVolumeIndexByTuple(cv).Compute(startIndex: first, endIndex: last),
            "NegativeVolumeIndex" => new NegativeVolumeIndexByTuple(cv).Compute(startIndex: first, endIndex: last),
            _ => new AccumulationDistributionLineByTuple(hlcv).Compute(startIndex: first, endIndex: last)
        });
}
