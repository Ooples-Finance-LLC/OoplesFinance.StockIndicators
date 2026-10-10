using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

public sealed class CpuFeasibilityConfig : ManualConfig
{
    public CpuFeasibilityConfig() => AddJob(Job.ShortRun
        .WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(10), true))
        .WithWarmupCount(5).WithIterationCount(10).WithUnrollFactor(1)
        .WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100)));
}

[MemoryDiagnoser, Config(typeof(CpuFeasibilityConfig))]
public class AsinFeasibilityBenchmarks
{
    [Params(1000, 10000, 100000)] public int Count { get; set; }
    private double[] _input = null!, _output = null!, _presence = null!;
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        _input = new CpuNativeWorkload("TaLib.Functions.Asin", Count, commonGrid: true).Data.Closes;
        _output = new double[Count]; _presence = new double[Count];
        _bars = _input.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        NativeReusable(); var expected = (double[])_output.Clone();
        UnrolledReusable(); RequireSame(expected, _output);
        ParallelReusable(); RequireSame(expected, _output);
        DefinedReusable(); RequireSame(expected, _output);
        if (_presence.Any(v => !v.Equals(1d))) throw new InvalidOperationException("Fixture is outside domain"); // NOSONAR: S1244 - binary presence flag must equal one exactly.
    }
    internal static void RequireSame(double[] expected, double[] actual)
    {
        for (var i = 0; i < expected.Length; i++)
            if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i]))
                throw new InvalidOperationException("Bit mismatch at " + i);
    }
    [Benchmark(Baseline = true)] public void NativeReusable() => Functions.Asin<double>(_input, System.Range.All, _output, out _);
    [Benchmark] public double[] NativeOwned() { var output = new double[Count]; Functions.Asin<double>(_input, System.Range.All, output, out _); return output; }
    [Benchmark] public void UnrolledReusable() => CpuFeasibilityPrototypes.AsinUnrolled(_input, _output);
    [Benchmark] public void ParallelReusable() => CpuFeasibilityPrototypes.AsinParallel(_input, _output);
    [Benchmark] public void DefinedReusable() => CpuFeasibilityPrototypes.AsinDefined(_input, _output, _presence);
    [Benchmark] public async Task<int> Builder()
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(new PriceCircularTransform(PriceCircularOperation.ArcSine)).BuildAsync();
        return run.BarCount;
    }
    [Benchmark] public async Task<int> IngestionOnly()
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).BuildAsync();
        return run.BarCount;
    }
}

[MemoryDiagnoser, Config(typeof(CpuFeasibilityConfig))]
public class SmaFeasibilityBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params(true, false)] public bool Grid { get; set; }
    private double[] _input = null!, _output = null!;
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        _input = new CpuNativeWorkload("TaLib.Functions.Sma", Count, commonGrid: Grid).Data.Closes;
        // The underlying SMA fixture can already lie on a binary grid even when
        // commonGrid is false. Deliberately exercise ordinary noncertified prices.
        if (!Grid) _input = _input.Select((value, i) => value + .013 * Math.Sin(i * .37 + .2)).ToArray();
        _output = new double[Count];
        if (Functions.Sma<double>(_input, System.Range.All, _output, out var range, 20) != TALib.Core.RetCode.Success
            || range.Start.Value != 19 || range.End.Value != Count)
            throw new InvalidOperationException("Native SMA range/status mismatch");
        var certified = CpuFeasibilityPrototypes.TrySmaVector(_input, _output, 20);
        if (certified != (Grid && System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated))
            throw new InvalidOperationException("Unexpected certificate eligibility for fixture");
        _bars = _input.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        CurrentReusable(); var expected = (double[])_output.Clone();
        VectorReusable(); AsinFeasibilityBenchmarks.RequireSame(expected, _output);
    }
    [Benchmark(Baseline = true)] public void NativeReusable() => Functions.Sma<double>(_input, System.Range.All, _output, out _, 20);
    [Benchmark] public double[] NativeOwned() { var output = new double[Count]; Functions.Sma<double>(_input, System.Range.All, output, out _, 20); return output; }
    [Benchmark] public void CurrentReusable() => CpuFeasibilityPrototypes.CurrentSma(_input, _output, 20);
    [Benchmark] public void VectorReusable() => CpuFeasibilityPrototypes.SmaVector(_input, _output, 20);
    [Benchmark] public async Task<int> Builder()
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(new Sma(20)).BuildAsync();
        return run.BarCount;
    }
    [Benchmark] public async Task<int> IngestionOnly()
    {
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).BuildAsync();
        return run.BarCount;
    }
}

// Diagnostic pipeline only: includes ownership, validation and both output
// columns, but excludes the builder graph and publication. Never a builder win.
[MemoryDiagnoser, Config(typeof(CpuFeasibilityConfig))]
public class AsinOwnershipFeasibilityBenchmarks
{
    [Params(1000, 10000, 100000)] public int Count { get; set; }
    private double[] _input = null!;
    private Bar[] _bars = null!;
    [GlobalSetup]
    public void Setup()
    {
        var data = new CpuNativeWorkload("TaLib.Functions.Asin", Count, commonGrid: true).Data;
        _input = data.Closes; _bars = data.IndicatorBars;
        var expected = NativeOwned();
        foreach (var parallel in new[] { false, true })
        foreach (var direct in new[] { false, true })
        {
            var actual = CpuFeasibilityPrototypes.AsinOwnedPipeline(_bars, parallel, direct);
            AsinFeasibilityBenchmarks.RequireSame(expected, actual.Values);
            if (actual.Presence.Any(v => !v.Equals(1d))) throw new InvalidOperationException("Presence mismatch"); // NOSONAR: S1244 - binary presence flag must equal one exactly.
        }
    }
    [Benchmark(Baseline = true)] public double[] NativeOwned()
    {
        var output = new double[Count];
        if (Functions.Asin<double>(_input, System.Range.All, output, out _) != TALib.Core.RetCode.Success)
            throw new InvalidOperationException("Native call failed");
        return output;
    }
    [Benchmark] public Bar[][] ArrayIngestionDirect() => CpuFeasibilityPrototypes.IngestArray(_bars, direct: true);
    [Benchmark] public (Bar[][], double[], double[]) SequentialOwnedDirect() => CpuFeasibilityPrototypes.AsinOwnedPipeline(_bars, false, direct: true);
    [Benchmark] public (Bar[][], double[], double[]) ParallelOwnedDirect() => CpuFeasibilityPrototypes.AsinOwnedPipeline(_bars, true, direct: true);
    [Benchmark] public Bar[][] ArrayIngestion() => CpuFeasibilityPrototypes.IngestArray(_bars);
    [Benchmark] public (Bar[][], double[], double[]) SequentialOwned() => CpuFeasibilityPrototypes.AsinOwnedPipeline(_bars, false);
    [Benchmark] public (Bar[][], double[], double[]) ParallelOwned() => CpuFeasibilityPrototypes.AsinOwnedPipeline(_bars, true);
}
