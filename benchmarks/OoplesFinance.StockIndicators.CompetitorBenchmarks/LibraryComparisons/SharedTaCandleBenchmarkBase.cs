using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

public abstract class SharedTaCandleBenchmarkBase : SharedMappedBenchmarkBase
{
    public abstract string Operation { get; set; }
    protected abstract int Lookback { get; }
    protected abstract int NativeLookback();
    protected abstract TALib.Core.RetCode ComputeNative(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> packed, out System.Range range);
    private double[] _open = null!, _high = null!, _low = null!, _close = null!;
    private int _workers;

    [GlobalSetup]
    public void Setup() => Setup(null);

    internal void Setup(CompetitorData? supplied)
    {
        if (NativeLookback() != Lookback) throw new InvalidOperationException("Native candle settings changed.");
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (pair, data) = PrepareFixture("TaLib.Candles." + Operation);
        if (supplied is not null)
        {
            if (supplied.Count != Count) throw new ArgumentException("Supplied fixture length differs.", nameof(supplied));
            data = supplied;
            _bars = data.IndicatorBars;
        }
        _open = data.Opens; _high = data.Highs; _low = data.Lows; _close = data.Closes;
        _workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, 20), ["Value"], includeScalarStartup: true);
        var expected = (pair.CompetitorReference ?? pair.Reference)!(data, 20).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeSingle().Select(v => (double)v).ToArray());
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeParallel().Select(v => (double)v).ToArray());
    }

    [Benchmark(Baseline = true)]
    public int[] NativeSingle()
    {
        var result = new int[Count];
        var (offset, count) = Invoke(0, Count, result);
        if (count > 0)
        {
            Array.Copy(result, 0, result, offset, count);
            Array.Clear(result, 0, offset);
        }
        return result;
    }

    [Benchmark]
    public int[] NativeParallel()
    {
        var result = new int[Count];
        CpuParallelSettings.LightweightParallel(_workers, _workers, worker =>
        {
            int start = (int)((long)Count * worker / _workers);
            int end = (int)((long)Count * (worker + 1) / _workers);
            int from = Math.Max(0, start - Lookback), length = end - from;
            var packed = new int[length];
            var (offset, count) = Invoke(from, length, packed);
            for (int i = 0; i < count; i++)
            {
                int index = from + offset + i;
                if (index >= start) result[index] = packed[i];
            }
        });
        return result;
    }

    private (int Offset, int Count) Invoke(int from, int length, int[] packed)
    {
        var open = _open.AsSpan(from, length); var high = _high.AsSpan(from, length);
        var low = _low.AsSpan(from, length); var close = _close.AsSpan(from, length);
        System.Range range;
        var code = ComputeNative(open, high, low, close, packed, out range);
        // The pinned API rejects a one-bar range; it has no publishable output.
        if (length == 1 && code == TALib.Core.RetCode.OutOfRangeParam) return (0, 0);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("Native candle failed: " + code);
        var (offset, count) = range.GetOffsetAndLength(length);
        if (count != Math.Max(0, length - Lookback) || count > 0 && offset != Lookback)
            throw new InvalidOperationException("Native candle output range differs.");
        return (offset, count);
    }
}
