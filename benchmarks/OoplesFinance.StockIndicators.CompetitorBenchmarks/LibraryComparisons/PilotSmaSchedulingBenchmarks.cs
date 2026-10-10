using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Native arms return compact, allocated values only. Parallel calls repeat the
// 19-bar lookback at each boundary; decimal rounding may differ from one call.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotSmaSchedulingBenchmarks
{
    [Params(10_000, 100_000)] public int Count { get; set; }
    [Params("Grid", "Decimal")] public string Shape { get; set; } = "";
    private double[] _close = null!;
    private Bar[] _bars = null!;

    [GlobalSetup]
    public void Setup()
    {
        _close = Enumerable.Range(0, Count).Select(i => Shape == "Grid"
            ? (i % 127 - 63) / 64d : 100 + i % 19 / 100d).ToArray();
        _bars = _close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var expected = NativeOwned();
        var parallel = NativeParallelOwned();
        for (int i = 0; i < Count - 19; i++)
        {
            if (Shape == "Grid")
            {
                if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(parallel[i]))
                    throw new InvalidOperationException("Parallel native grid SMA mismatch.");
            }
            else if (!double.IsFinite(parallel[i]) || Math.Abs(expected[i] - parallel[i]) > 1e-10)
                throw new InvalidOperationException("Parallel native SMA mismatch.");
        }
        var sma = new Sma(20);
        using var actual = Build(sma);
        var guarded = new double[Count];
        CpuFeasibilityPrototypes.CurrentSma(_close, guarded, 20);
        AsinFeasibilityBenchmarks.RequireSame(guarded, actual[sma].ToArray());
    }

    private IIndicatorRun Build(Sma sma) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(sma)
        .ConfigureHistory(IndicatorHistoryMode.LatestOnly).BuildAsync().GetAwaiter().GetResult();

    [Benchmark] public IIndicatorRun Builder() => Build(new Sma(20));

    [Benchmark(Baseline = true)]
    public double[] NativeOwned()
    {
        var output = new double[Count];
        Functions.Sma<double>(_close, System.Range.All, output, out _, 20);
        return output;
    }

    [Benchmark]
    public double[] NativeParallelOwned()
    {
        var output = new double[Count];
        int chunks = Math.Min(8, Environment.ProcessorCount);
        int defined = Count - 19;
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = (int)((long)defined * chunk / chunks);
            int end = (int)((long)defined * (chunk + 1) / chunks);
            Functions.Sma<double>(_close.AsSpan(start, end - start + 19), System.Range.All,
                output.AsSpan(start, end - start), out _, 20);
        });
        return output;
    }
}
