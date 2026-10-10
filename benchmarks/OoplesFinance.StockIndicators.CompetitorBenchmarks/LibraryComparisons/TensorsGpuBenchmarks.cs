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
        if (Shape == "Asin") _close[0] = -0d;
        _bars = _close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var native = Native();
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var gpu = Execute(IndicatorExecutionBackend.Gpu, history);
            using var gpuRun = gpu.Run;
            Console.WriteLine($"GPU setup first call in this case: {Shape}, {Count}, {history}, {timer.Elapsed.TotalMilliseconds:F3} ms (process/driver caches may already be warm).");
            var cpu = Execute(IndicatorExecutionBackend.Cpu, history);
            using var cpuRun = cpu.Run;
            for (var slot = 0; slot < cpu.Indicator.Outputs.Count; slot++)
            for (var i = 0; i < Count; i++)
            {
                var expected = cpuRun[cpu.Indicator.Outputs[slot]][i];
                var actual = gpuRun[gpu.Indicator.Outputs[slot]][i];
                if (Shape == "Sma" || slot > 0)
                {
                    if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(actual))
                        throw new InvalidOperationException("Exact SMA/presence output differs.");
                }
                else Check(expected, actual);
                if (slot == 0 && (Shape == "Asin" || i >= 19))
                    Check(native[Shape == "Asin" ? i : i - 19], actual);
            }
        }
    }

    private static void Check(double expected, double actual)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 4e-15 * Math.Abs(expected)
            || (expected.Equals(0d) && BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(actual)))
            throw new InvalidOperationException($"GPU benchmark parity failed: {expected:R} / {actual:R}");
    }

    private (IIndicatorRun Run, IIndicator Indicator) Execute(IndicatorExecutionBackend backend, IndicatorHistoryMode history)
    {
        IIndicator indicator = new Sma(20);
        if (Shape != "Sma")
        {
            var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            if (Shape == "SmaAsin") asin.Of(indicator);
            indicator = asin;
        }
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(indicator).ConfigureExecution(backend).ConfigureHistory(history);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (builder.LastExecution?.Backend != backend) throw new InvalidOperationException("Unexpected backend.");
        return (run, indicator);
    }

    [Benchmark] public IIndicatorRun CpuBuilder() => Execute(IndicatorExecutionBackend.Cpu, IndicatorHistoryMode.Full).Run;
    [Benchmark] public IIndicatorRun GpuBuilder() => Execute(IndicatorExecutionBackend.Gpu, IndicatorHistoryMode.Full).Run;
    [Benchmark] public IIndicatorRun CpuLatestOnlyBuilder() => Execute(IndicatorExecutionBackend.Cpu, IndicatorHistoryMode.LatestOnly).Run;
    [Benchmark] public IIndicatorRun GpuLatestOnlyBuilder() => Execute(IndicatorExecutionBackend.Gpu, IndicatorHistoryMode.LatestOnly).Run;
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
