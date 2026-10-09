using System.Runtime.Intrinsics;
using AiDotNet.Tensors.Operators;
using BenchmarkDotNet.Attributes;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Arithmetic qualification only. This does not replace the fresh-builder gate.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotAsinQualificationBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    private double[] _input = null!;
    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(971);
        _input = Enumerable.Range(0, Count).Select(_ => random.NextDouble() * 2 - 1).ToArray();
        new[] { -0d, 0d, -1d, 1d, double.Epsilon, -double.Epsilon, Math.BitDecrement(1d), Math.BitIncrement(-1d) }.CopyTo(_input, 0);
        AsinFeasibilityBenchmarks.RequireSame(Scalar(), TensorsVector());
        AsinFeasibilityBenchmarks.RequireSame(Scalar(), Competitor());
    }

    [Benchmark] public double[] Scalar()
    {
        var output = new double[Count];
        for (var i = 0; i < Count; i++) output[i] = Math.Asin(_input[i]);
        return output;
    }

    [Benchmark] public double[] TensorsVector()
    {
        var output = new double[Count];
        var operation = new AsinOperatorDouble();
        int i = 0;
        for (; i <= Count - Vector256<double>.Count; i += Vector256<double>.Count)
            operation.Invoke(Vector256.LoadUnsafe(ref _input[0], (nuint)i)).StoreUnsafe(ref output[0], (nuint)i);
        for (; i < Count; i++) output[i] = operation.Invoke(_input[i]);
        return output;
    }

    [Benchmark(Baseline = true)] public double[] Competitor()
    {
        var output = new double[Count];
        Functions.Asin<double>(_input, System.Range.All, output, out _);
        return output;
    }
}
