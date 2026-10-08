using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal static class CpuKernelPilots
{
    internal static readonly string[] Ids = ["QuanTAlib.Jma", "QuanTAlib.Atr",
        "Skender.GetRollingPivots", "Skender.GetFractal", "TaLib.Candles.RickshawMan",
        "TaLib.Functions.Asin", "Trady.Candlestick.BullishShortDay", "Trady.Indicator.SimpleMovingAverage"];
    internal static IndicatorKernel Create(string id, int period = 20) => id switch
    {
        "QuanTAlib.Jma" => IndicatorKernels.Jurik(period),
        "QuanTAlib.Atr" => IndicatorKernels.ScaledTrueRange(period),
        "Skender.GetRollingPivots" => IndicatorKernels.RollingPivots(period),
        "Skender.GetFractal" => IndicatorKernels.Fractal(period, period),
        "TaLib.Candles.RickshawMan" => IndicatorKernels.RickshawMan(),
        "TaLib.Functions.Asin" => IndicatorKernels.Asin(),
        "Trady.Candlestick.BullishShortDay" => IndicatorKernels.BullishShortBody(period),
        "Trady.Indicator.SimpleMovingAverage" => IndicatorKernels.Sma(period),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    internal static void Verify(string id, CompetitorData data, IndicatorKernel kernel, double[] output, int period = 20)
    {
        kernel.Reset(); kernel.Process(data.IndicatorBars, output);
        var pair = ComparisonPairs.Get(id);
        var expected = pair.Ooples(data, period);
        var names = pair.OutputNames ?? ["Value"];
        for (var i = 0; i < data.Count; i++)
        {
            var source = id == "Skender.GetFractal" ? i - period : i;
            for (var slot = 0; slot < names.Length; slot++)
            {
                var column = expected.Outputs[names[slot]];
                var value = source < 0 || column.Present is not null && !column.Present[source]
                    ? double.NaN : column.Values[source];
                var actual = output[i * kernel.OutputCount + slot];
                if (!actual.Equals(value))
                    throw new InvalidOperationException($"{id} kernel differs at row {i}, slot {slot}: {actual:R} != {value:R}.");
            }
        }
        kernel.Reset();
    }
}

/// <summary>End-to-end paired APIs alongside explicitly separate reusable batch/streaming routes.</summary>
[MemoryDiagnoser]
[Config(typeof(CpuKernelTimingConfig))]
public class CpuKernelBenchmarks
{
    public IEnumerable<string> Cases => Environment.GetEnvironmentVariable("COMPARISON_PAIR") is { } id
        ? CpuKernelPilots.Ids.Where(value => value == id) : CpuKernelPilots.Ids;
    [ParamsSource(nameof(Cases))] public string PairId { get; set; } = "";
    [Params(1_000, 10_000)] public int Bars { get; set; }
    private ComparisonPair _pair = null!;
    private CompetitorData _data = null!;
    private IndicatorKernel _kernel = null!;
    private double[] _output = null!;
    [GlobalSetup]
    public void Setup()
    {
        _pair = ComparisonPairs.Get(PairId);
        _data = ComparisonVerifier.BenchmarkFixture(_pair, Bars);
        _kernel = CpuKernelPilots.Create(PairId);
        _output = new double[Bars * _kernel.OutputCount];
        // Use a bounded independent formula check in every setup; the complete
        // timed fixture is checked against the public route below. Quadratic
        // competitor setup must not be repeated for each reusable-kernel arm.
        ComparisonVerifier.Check(_pair, ComparisonVerifier.BenchmarkFixture(_pair, 160), 20, verifyIsolation: false);
        CpuKernelPilots.Verify(PairId, _data, _kernel, _output);
    }
    [Benchmark(Baseline = true)] public object PublicApi() => _pair.Ooples(_data, 20);
    [Benchmark] public object Competitor() => _pair.Competitor(_data, 20);
    [Benchmark] public double[] CpuBatch()
    {
        _kernel.Reset(); _kernel.Process(_data.IndicatorBars, _output); return _output;
    }
    [Benchmark] public double[] CpuStreaming()
    {
        _kernel.Reset();
        for (var i = 0; i < _data.Count; i++)
            _kernel.Update(_data.IndicatorBars[i], _output.AsSpan(i * _kernel.OutputCount, _kernel.OutputCount));
        return _output;
    }
}

public sealed class CpuKernelTimingConfig : ManualConfig
{
    public CpuKernelTimingConfig()
    {
        // A single invocation measures tier transitions for fast kernels. Warm
        // ordinary pairs with repeated invocations; keep the quadratic Trady
        // API bounded when selected (or when running the whole catalog at once).
        var pair = Environment.GetEnvironmentVariable("COMPARISON_PAIR");
        var slow = pair is null or "Trady.Candlestick.BullishShortDay";
        AddJob(Job.ShortRun.WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(30), true))
            .WithInvocationCount(slow ? 1 : 16).WithUnrollFactor(1)
            .WithWarmupCount(slow ? 3 : 8).WithIterationCount(slow ? 3 : 5));
    }
}
