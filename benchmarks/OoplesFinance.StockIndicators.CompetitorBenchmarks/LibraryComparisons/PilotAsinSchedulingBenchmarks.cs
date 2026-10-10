using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Keep the native single-call baseline and a caller-parallelized native baseline
// visible together. Both native arms omit bar validation, presence and snapshots.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotAsinSchedulingBenchmarks
{
    [Params(10_000, 65_536, 100_000, 1_000_000)] public int Count { get; set; }
    [Params("Grid", "Random")] public string Shape { get; set; } = "";
    private double[] _close = null!;
    private Bar[] _bars = null!;

    [GlobalSetup]
    public void Setup()
    {
#pragma warning disable S2245 // Reproducible numerical fixture, not security-sensitive randomness.
        var random = new Random(981);
#pragma warning restore S2245
        _close = Enumerable.Range(0, Count).Select(i => Shape == "Grid"
            ? (i % 127 - 63) / 64d : random.NextDouble() * 2 - 1).ToArray();
        _close[0] = -0d;
        _bars = _close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var expected = NativeOwned();
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeParallelOwned());
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        using var actual = Build(asin);
        AsinFeasibilityBenchmarks.RequireSame(expected, actual[asin.Value].ToArray());
        for (int i = 0; i < Count; i++)
            if (BitConverter.DoubleToInt64Bits(actual[asin.IsDefined][i]) != BitConverter.DoubleToInt64Bits(1d))
                throw new InvalidOperationException("Asin presence mismatch.");
    }

    private IIndicatorRun Build(PriceCircularTransform asin) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(asin)
        .ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult();

    [Benchmark]
    public IIndicatorRun Builder() => Build(new PriceCircularTransform(PriceCircularOperation.ArcSine));

    [Benchmark(Baseline = true)]
    public double[] NativeOwned()
    {
        var output = new double[Count];
        Functions.Asin<double>(_close, System.Range.All, output, out _);
        return output;
    }

    [Benchmark]
    public double[] NativeParallelOwned()
    {
        var output = new double[Count];
        int chunks = Math.Min(4, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = (int)((long)Count * chunk / chunks);
            int end = (int)((long)Count * (chunk + 1) / chunks);
            Functions.Asin<double>(_close.AsSpan(start, end - start), System.Range.All,
                output.AsSpan(start, end - start), out _);
        });
        return output;
    }
}
