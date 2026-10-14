using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRickshawBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _open = null!, _high = null!, _low = null!, _close = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 2) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            var c = KickingRickshawComparison.RickshawCandidates[i % KickingRickshawComparison.RickshawCandidates.Length];
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), c.O, c.H, c.L, c.C, 1);
        }).ToArray();
        _open = _bars.Select(b => b.Open).ToArray(); _high = _bars.Select(b => b.High).ToArray();
        _low = _bars.Select(b => b.Low).ToArray(); _close = _bars.Select(b => b.Close).ToArray();
        var data = KickingRickshawComparison.FromBars(_bars);
        var pair = ComparisonPairs.Get("TaLib.Candles.RickshawMan");
        var expected = pair.Reference!(data, 20).Outputs["Value"].Values;
        var serial = NativeSingle(); var parallel = NativeParallel();
        AsinFeasibilityBenchmarks.RequireSame(expected, serial.Select(v => (double)v).ToArray());
        AsinFeasibilityBenchmarks.RequireSame(expected, parallel.Select(v => (double)v).ToArray());
        if (Count >= 1000 && (!serial.Contains(100) || !serial.Skip(10).Contains(0)))
            throw new InvalidOperationException("Rickshaw fixture must include matches and nonmatches.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new RickshawManCandle(10, 5);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(10, 5), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(10, 5), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public int[] NativeSingle()
    {
        var result = new int[Count];
        var code = Candles.RickshawMan<double>(_open, _high, _low, _close, System.Range.All, result, out var range);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("Native Rickshaw failed.");
        var (offset, count) = range.GetOffsetAndLength(Count);
        if (count != Math.Max(0, Count - 10) || count > 0 && offset != 10)
            throw new InvalidOperationException("Native Rickshaw range changed.");
        if (count > 0) { Array.Copy(result, 0, result, offset, count); Array.Clear(result, 0, offset); }
        return result;
    }
    [Benchmark] public int[] NativeParallel()
    {
        int workers = Math.Min(Count / 2, Math.Min(8, Environment.ProcessorCount));
        var result = new int[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, int[] result)
    {
        int from = Math.Max(0, start - 10), length = end - from;
        var packed = new int[length];
        var code = Candles.RickshawMan<double>(_open.AsSpan(from, length), _high.AsSpan(from, length),
            _low.AsSpan(from, length), _close.AsSpan(from, length), System.Range.All, packed, out var range);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("Native Rickshaw failed.");
        var (offset, count) = range.GetOffsetAndLength(length);
        if (count != Math.Max(0, length - 10) || count > 0 && offset != 10)
            throw new InvalidOperationException("Native Rickshaw range changed.");
        for (int i = 0; i < count; i++)
        {
            int index = from + offset + i;
            if (index >= start) result[index] = packed[i];
        }
    }
    private IIndicatorRun Build(RickshawManCandle indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
