using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Complete builder ownership/upload/launch/readback versus native owned outputs.
// Native does not pay for bar history or presence flags. Setup verifies all values.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class TensorsGpuBenchmarks
{
    [Params(1_000, 10_000, 100_000, 1_000_000)] public int Count { get; set; }
    [Params("Sma", "Asin", "SmaAsin")] public string Shape { get; set; } = "";
    private Bar[] _bars = null!;
    private double[] _close = null!;

    [GlobalSetup]
    public void Setup()
    {
        _close = Enumerable.Range(0, Count).Select(i => (i % 127 - 63) / 64d).ToArray();
        _bars = _close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var gpu = Execute(IndicatorExecutionBackend.Gpu, true);
        Console.WriteLine($"GPU setup first call in this case: {Shape}, {Count}, {timer.Elapsed.TotalMilliseconds:F3} ms (process/driver caches may already be warm).");
        var cpu = Execute(IndicatorExecutionBackend.Cpu, true);
        var native = Native();
        for (var i = 0; i < Count; i++)
        {
            Check(cpu![i], gpu![i]);
            if (Shape == "Asin" || i >= 19) Check(native[Shape == "Asin" ? i : i - 19], gpu[i]);
        }
    }

    private static void Check(double expected, double actual)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 4e-15 * Math.Abs(expected))
            throw new InvalidOperationException($"GPU benchmark parity failed: {expected:R} / {actual:R}");
    }

    private double[]? Execute(IndicatorExecutionBackend backend, bool copy = false)
    {
        IIndicator indicator = new Sma(20);
        if (Shape != "Sma")
        {
            var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            if (Shape == "SmaAsin") asin.Of(indicator);
            indicator = asin;
        }
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(indicator).ConfigureExecution(backend);
        using var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (builder.LastExecution?.Backend != backend) throw new InvalidOperationException("Unexpected backend.");
        return copy ? run[indicator.Outputs[0]].ToArray() : null;
    }

    [Benchmark] public void CpuBuilder() => Execute(IndicatorExecutionBackend.Cpu);
    [Benchmark] public void GpuBuilder() => Execute(IndicatorExecutionBackend.Gpu);
    [Benchmark(Baseline = true)] public double[] Native()
    {
        var output = new double[Count];
        if (Shape == "Asin") Functions.Asin<double>(_close, System.Range.All, output, out _);
        else
        {
            Functions.Sma<double>(_close, System.Range.All, output, out _, 20);
            if (Shape == "SmaAsin")
            {
                var transformed = new double[Count - 19];
                Functions.Asin<double>(output.AsSpan(0, Count - 19), System.Range.All, transformed, out _);
                return transformed;
            }
        }
        return output;
    }
}

public sealed class TensorsGpuTimingConfig : ManualConfig
{
    public TensorsGpuTimingConfig() => AddJob(Job.ShortRun
        .WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(10), true))
        .WithUnrollFactor(1).WithWarmupCount(3).WithIterationCount(5)
        .WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100)));
}
