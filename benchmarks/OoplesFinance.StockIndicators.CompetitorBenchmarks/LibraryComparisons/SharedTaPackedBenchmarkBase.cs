using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using System.Numerics;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

public abstract class SharedTaPackedBenchmarkBase : SharedTaPackedBenchmarkBase<double> { }

public abstract class SharedTaPackedBenchmarkBase<T> : SharedMappedBenchmarkBase where T : INumber<T>
{
    private protected CompetitorData Data { get; private set; } = null!;
    protected abstract string Mapping { get; }
    protected abstract int Lookback { get; }
    protected abstract int Columns { get; }
    protected virtual bool IndependentWindows => false;
    protected abstract TALib.Core.RetCode Call(System.Range input, T[][] output, out System.Range range);
    protected virtual void PrepareNative() { }
    private protected virtual void VerifyBuilder(ComparisonPair pair, string[] names) =>
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(Data, 20), names);

    [GlobalSetup]
    public void Setup()
    {
        var (pair, data) = PrepareFixture(Mapping);
        Data = data;
        PrepareNative();
        var names = pair.OutputNames ?? ["Value"];
        var native = (pair.CompetitorReference ?? pair.Reference)!(data, 20);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        for (int slot = 0; slot < names.Length; slot++)
        {
            var expected = native.Outputs[names[slot]].Values.Skip(Lookback).ToArray();
            AsinFeasibilityBenchmarks.RequireSame(expected, serial[slot].Select(v => double.CreateChecked(v)).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected, parallel[slot].Select(v => double.CreateChecked(v)).ToArray());
        }
        VerifyBuilder(pair, names);
    }

    [Benchmark(Baseline = true)]
    public T[][] NativeSingle() => NativeRange(0, Math.Max(0, Count - Lookback));

    private T[][] NativeRange(int start, int end)
    {
        int defined = end - start;
        var output = Allocate(defined);
        if (defined == 0) return output;
        var code = Call(new System.Range(start + Lookback, end + Lookback - 1), output, out var range);
        if (code != TALib.Core.RetCode.Success || !range.Equals(new System.Range(start + Lookback, end + Lookback)))
            throw new InvalidOperationException("Unexpected native packed result alignment.");
        return output;
    }

    private T[][] Allocate(int length) => Enumerable.Range(0, Columns).Select(_ => new T[length]).ToArray();

    [Benchmark]
    public T[][] NativeParallel()
    {
        int defined = Math.Max(0, Count - Lookback);
        var output = Allocate(defined);
        int workers = Math.Min(defined, Math.Min(8, Environment.ProcessorCount));
        if (workers == 0) return output;
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = defined * worker / workers, end = defined * (worker + 1) / workers;
            // Recursive and rolling native arithmetic retains the original prefix.
            int first = IndependentWindows ? start : 0;
            var prefix = NativeRange(first, end);
            for (int slot = 0; slot < output.Length; slot++)
                prefix[slot].AsSpan(start - first, end - start).CopyTo(output[slot].AsSpan(start));
        });
        return output;
    }
}
