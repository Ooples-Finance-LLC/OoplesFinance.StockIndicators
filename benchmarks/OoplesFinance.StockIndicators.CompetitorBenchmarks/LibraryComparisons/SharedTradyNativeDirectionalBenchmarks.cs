using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyNativeDirectionalBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("PlusDirectionalMovement", "MinusDirectionalMovement", "PlusDirectionalIndicator", "MinusDirectionalIndicator",
        "DirectionalMovementIndex", "AverageDirectionalIndex", "AverageDirectionalIndexRating")]
    public string Operation { get; set; } = "";
    private DirectionalWindowMeasure _measure;
    private decimal[] _highs = null!, _lows = null!;
    private (decimal High, decimal Low, decimal Close)[] _hlc = null!;
    private IEnumerable<decimal>[] _highWindows = null!, _lowWindows = null!;
    private IEnumerable<(decimal High, decimal Low, decimal Close)>[] _hlcWindows = null!;
    private int[] _ends = null!, _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        int index = Array.IndexOf(DirectionalComparison.Names, Operation);
        if (index < 0) throw new ArgumentException("Unknown Trady directional operation.");
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        _measure = (DirectionalWindowMeasure)index;
        var (pair, data) = PrepareFixture("Trady.Indicator." + Operation);
        _highs = data.Candles.Select(c => c.High).ToArray();
        _lows = data.Candles.Select(c => c.Low).ToArray();
        _hlc = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Raw movement is a one-bar difference. Wilder-smoothed measures
        // retain their full prefixes, including their distinct startup seeds.
        _seeds = Enumerable.Range(0, workers).Select(i => index < 2
            ? Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 1) : 0).ToArray();
        _highWindows = _ends.Select((end, i) => _highs.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _lowWindows = _ends.Select((end, i) => _lows.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _hlcWindows = _ends.Select((end, i) => _hlc.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, Period), ["Value"]);
        var expected = DirectionalComparison.DecimalReference(data, Period, Period)[index];
        if (!expected.SequenceEqual(NativeSingle()) || !expected.SequenceEqual(NativeParallel()))
            throw new InvalidOperationException("Native directional decimal values or presence differ from their independent reference.");
    }

    protected override IIndicator Create() => new WindowDirectionalMeasure(_measure, Period, Period);

    [Benchmark(Baseline = true)]
    public List<decimal?> NativeSingle() => Compute(_highs, _lows, _hlc);

    [Benchmark]
    public decimal?[] NativeParallel()
    {
        var joined = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            var rows = Compute(_highWindows[worker], _lowWindows[worker], _hlcWindows[worker],
                first - _seeds[worker], _ends[worker] - _seeds[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native directional range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    private List<decimal?> Compute(IEnumerable<decimal> highs, IEnumerable<decimal> lows,
        IEnumerable<(decimal High, decimal Low, decimal Close)> hlc, int? first = null, int? last = null) =>
        (List<decimal?>)(_measure switch
        {
            DirectionalWindowMeasure.PositiveMovement => new PlusDirectionalMovementByTuple(highs).Compute(startIndex: first, endIndex: last),
            DirectionalWindowMeasure.NegativeMovement => new MinusDirectionalMovementByTuple(lows).Compute(startIndex: first, endIndex: last),
            DirectionalWindowMeasure.PositiveIndicator => new PlusDirectionalIndicatorByTuple(hlc, Period).Compute(startIndex: first, endIndex: last),
            DirectionalWindowMeasure.NegativeIndicator => new MinusDirectionalIndicatorByTuple(hlc, Period).Compute(startIndex: first, endIndex: last),
            DirectionalWindowMeasure.Index => new DirectionalMovementIndexByTuple(hlc, Period).Compute(startIndex: first, endIndex: last),
            DirectionalWindowMeasure.EarlyAverage => new AverageDirectionalIndexByTuple(hlc, Period).Compute(startIndex: first, endIndex: last),
            _ => new AverageDirectionalIndexRatingByTuple(hlc, Period, Period).Compute(startIndex: first, endIndex: last)
        });
}
