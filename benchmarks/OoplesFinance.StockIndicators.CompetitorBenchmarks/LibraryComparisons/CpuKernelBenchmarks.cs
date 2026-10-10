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
        VerifyOutput(id, data, kernel.OutputCount, output, period);
        kernel.Reset();
    }
    internal static void VerifyOutput(string id, CompetitorData data, int outputCount, double[] output, int period = 20, bool aligned = false)
    {
        var pair = ComparisonPairs.Get(id);
        var expected = pair.Ooples(data, period);
        var names = pair.OutputNames ?? ["Value"];
        for (var i = 0; i < data.Count; i++)
        {
            var source = id == "Skender.GetFractal" && !aligned ? i - period : i;
            for (var slot = 0; slot < names.Length; slot++)
            {
                var column = expected.Outputs[names[slot]];
                var value = source < 0 || column.Present is not null && !column.Present[source]
                    ? double.NaN : column.Values[source];
                var actual = output[i * outputCount + slot];
                if (!actual.Equals(value)) // NOSONAR: the kernel contract requires exact values, including matching NaNs.
                    throw new InvalidOperationException($"{id} kernel differs at row {i}, slot {slot}: {actual:R} != {value:R}.");
            }
        }
    }
}

/// <summary>Direct native comparisons with matched state/output ownership boundaries.</summary>
[MemoryDiagnoser]
[Config(typeof(CpuKernelTimingConfig))]
public class CpuKernelBenchmarks
{
    public static IEnumerable<string> Cases => Environment.GetEnvironmentVariable("COMPARISON_PAIR") is { } id
        ? CpuKernelPilots.Ids.Where(value => value == id) : CpuKernelPilots.Ids;
    [ParamsSource(nameof(Cases))] public string PairId { get; set; } = "";
    [Params(1_000, 10_000)] public int Bars { get; set; }
    private CpuNativeWorkload _work = null!;
    private IndicatorKernel _kernel = null!;
    private double[] _output = null!;
    private IndicatorKernel[] _streams = null!;
    private QuanTAlib.AbstractBase[] _nativeStreams = null!;
    private double[][] _streamOutput = null!;
    internal const int StreamCount = 64;

    private static readonly HashSet<string> Verified = new();
    [GlobalSetup]
    public void Setup()
    {
        _work = new CpuNativeWorkload(PairId, Bars);
        _kernel = CpuKernelPilots.Create(PairId);
        _output = new double[Bars * _kernel.OutputCount];
        lock (Verified)
        {
            var key = PairId + ":" + Bars;
            if (!Verified.Contains(key)) { _work.Verify(); Verified.Add(key); }
        }
    }
    [IterationSetup(Targets = new[] { nameof(OoplesStreaming), nameof(CompetitorStreaming) })]
    public void SetupStreams()
    {
        // Neither library's reset semantics are assumed. Each measured sequence
        // starts with fresh state and caller storage, outside the timed region.
        _streams = new IndicatorKernel[StreamCount];
        _nativeStreams = new QuanTAlib.AbstractBase[StreamCount];
        _streamOutput = new double[StreamCount][];
        for (var i = 0; i < StreamCount; i++)
        {
            _streams[i] = CpuKernelPilots.Create(PairId);
            _nativeStreams[i] = _work.NewNativeStream();
            _streamOutput[i] = new double[Bars];
        }
    }
    [Benchmark] public double[] OoplesOwnedBatch() => _work.OoplesOwned();
    [Benchmark] public object CompetitorOwnedBatch() => _work.NativeOwned();
    [Benchmark] public double[] OoplesReusableBatch()
    {
        if (PairId == "TaLib.Functions.Asin") IndicatorKernels.Asin(_work.Data.Closes, _output);
        else { _kernel.Reset(); _kernel.Process(_work.Data.IndicatorBars, _output); }
        return _output;
    }
    [Benchmark] public object CompetitorReusableBatch() => _work.NativeReusable();
    [Benchmark(OperationsPerInvoke = StreamCount)] public double[][] OoplesStreaming()
    {
        for (var stream = 0; stream < StreamCount; stream++)
            _work.RunOoplesStream(_streams[stream], _streamOutput[stream]);
        return _streamOutput;
    }
    [Benchmark(OperationsPerInvoke = StreamCount)] public double[][] CompetitorStreaming()
    {
        for (var stream = 0; stream < StreamCount; stream++)
            _work.RunNativeStream(_nativeStreams[stream], _streamOutput[stream]);
        return _streamOutput;
    }
}

public sealed class CpuKernelTimingConfig : ManualConfig
{
    public CpuKernelTimingConfig()
    {
        var pair = Environment.GetEnvironmentVariable("COMPARISON_PAIR");
        var slow = pair is null or "Trady.Candlestick.BullishShortDay";
        var job = Job.ShortRun.WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(30), true))
            .WithUnrollFactor(1).WithWarmupCount(slow ? 3 : 8).WithIterationCount(slow ? 3 : 5);
        AddJob((slow ? job.WithInvocationCount(1)
            : job.WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100))).WithId("Batch"));
        AddJob(job.WithInvocationCount(1).WithId("Streaming"));
        AddFilter(new NativeCapabilityFilter());
    }
    private sealed class NativeCapabilityFilter : BenchmarkDotNet.Filters.IFilter
    {
        public bool Predicate(BenchmarkDotNet.Running.BenchmarkCase benchmarkCase)
        {
            var pair = (string)benchmarkCase.Parameters.Items.Single(p => p.Name == "PairId").Value;
            var method = benchmarkCase.Descriptor.WorkloadMethod.Name;
            if (method.Contains("Streaming", StringComparison.Ordinal))
                return benchmarkCase.Job.Id == "Streaming" && CpuNativeWorkload.SupportsStreaming(pair);
            if (benchmarkCase.Job.Id != "Batch") return false;
            return !method.Contains("Reusable", StringComparison.Ordinal) || CpuNativeWorkload.SupportsReusable(pair);
        }
    }
}
