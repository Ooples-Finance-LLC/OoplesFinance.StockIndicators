using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedAsinBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    private double[] _close = null!;
    private Bar[] _bars = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 2) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _close = Enumerable.Range(0, Count).Select(i => (i % 127 - 63) / 64d).ToArray();
        double[] edges = [-0d, 0d, -1d, 1d, double.Epsilon, -double.Epsilon, Math.BitDecrement(1d), Math.BitIncrement(-1d)];
        edges.AsSpan(0, Math.Min(edges.Length, Count)).CopyTo(_close);
        _bars = _close.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 2, i - 1, v, 1)).ToArray();
        var expected = _close.Select(Math.Asin).ToArray();
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeSingle());
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeParallel());
        foreach (double value in _close.Distinct())
        {
            double reference = CircularComparison.ReferenceValue("Asin", value)!.Value, actual = Math.Asin(value);
            if (!double.IsFinite(actual) || Math.Abs(reference - actual) > Math.Abs(reference) * 4e-15
                || Math.Sign(reference) != Math.Sign(actual))
                throw new InvalidOperationException("Independent Asin reference disagrees.");
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(Enumerable.Repeat(1d, Count).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(PriceCircularOperation.ArcSine), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(PriceCircularOperation.ArcSine), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = new double[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        // TA-Lib requires at least two inputs in each call.
        int workers = Math.Min(Count / 2, Math.Min(8, Environment.ProcessorCount));
        var result = new double[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, double[] result)
    {
        int length = end - start;
        var code = Functions.Asin<double>(_close.AsSpan(start, length), System.Range.All, result.AsSpan(start, length), out var range);
        if (code != TALib.Core.RetCode.Success || range.GetOffsetAndLength(length) != (0, length))
            throw new InvalidOperationException("Native Asin status/range failed.");
    }
    private IIndicatorRun Build(PriceCircularTransform indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
