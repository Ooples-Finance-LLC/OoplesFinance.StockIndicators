using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>Common full-output and overlapping-chunk harness for TA-Lib candles.</summary>
public abstract class SharedCandleBenchmarkBase
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    protected string _name = null!;
    protected abstract int Warmup { get; }
    private Bar[] _bars = null!;
    private double[] _open = null!, _high = null!, _low = null!, _close = null!;
    protected int _priorDop;

    // Fixture creation and verification stay outside the measured methods.
    protected (double[] Expected, int[] Serial) InitializeFixture(Bar[] fixture, string operation)
    {
        if (Count < 2) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            var c = fixture[i % fixture.Length];
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), c.Open, c.High, c.Low, c.Close, 1);
        }).ToArray();
        _open = _bars.Select(b => b.Open).ToArray(); _high = _bars.Select(b => b.High).ToArray();
        _low = _bars.Select(b => b.Low).ToArray(); _close = _bars.Select(b => b.Close).ToArray();
        var data = KickingRickshawComparison.FromBars(_bars);
        var pair = ComparisonPairs.Get(operation);
        var expected = pair.Reference!(data, 20).Outputs["Value"].Values;
        var serial = NativeSingle(); var parallel = NativeParallel();
        AsinFeasibilityBenchmarks.RequireSame(expected, serial.Select(v => (double)v).ToArray());
        AsinFeasibilityBenchmarks.RequireSame(expected, parallel.Select(v => (double)v).ToArray());
        return (expected, serial);
    }

    protected void VerifyBuilder(double[] expected, Func<IIndicator> create)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Outputs[0]].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark(Baseline = true)] public int[] NativeSingle()
    {
        var result = new int[Count];
        var code = Call(_open, _high, _low, _close, result, out var range);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException($"Native {GetType().Name} failed.");
        var (offset, count) = range.GetOffsetAndLength(Count);
        if (count != Math.Max(0, Count - Warmup) || count > 0 && offset != Warmup)
            throw new InvalidOperationException($"Native {GetType().Name} range changed.");
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
        int from = Math.Max(0, start - Warmup), length = end - from;
        var packed = new int[length];
        var code = Call(_open.AsSpan(from, length), _high.AsSpan(from, length),
            _low.AsSpan(from, length), _close.AsSpan(from, length), packed, out var range);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException($"Native {GetType().Name} failed.");
        var (offset, count) = range.GetOffsetAndLength(length);
        if (count != Math.Max(0, length - Warmup) || count > 0 && offset != Warmup)
            throw new InvalidOperationException($"Native {GetType().Name} range changed.");
        for (int i = 0; i < count; i++)
        {
            int index = from + offset + i;
            if (index >= start) result[index] = packed[i];
        }
    }
    protected abstract TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range);

    protected IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) =>
        SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
