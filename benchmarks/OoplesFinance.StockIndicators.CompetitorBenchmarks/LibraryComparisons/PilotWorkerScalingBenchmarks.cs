using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Per-case process setting is restored after measurement. Both parallel arms
// get the same available worker budget; the builder can select a lower cap.
// Native arms own values only, without validation or history.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotWorkerScalingBenchmarks
{
    [Params(100_000)] public int Count { get; set; }
    [Params(1, 2, 4, 8)] public int Workers { get; set; }
    [Params("SmaGrid", "SmaDecimal", "AsinGrid", "AsinRandom")] public string Case { get; set; } = "";
    private double[] _close = null!;
    private Bar[] _bars = null!;
    private int _previousDop;
    private bool IsSma => Case.StartsWith("Sma", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = Workers;
#pragma warning disable S2245 // Reproducible benchmark fixture.
        var random = new Random(981);
#pragma warning restore S2245
        _close = Enumerable.Range(0, Count).Select(i => Case switch
        {
            "SmaDecimal" => 100 + i % 19 / 100d,
            "AsinRandom" => random.NextDouble() * 2 - 1,
            _ => (i % 127 - 63) / 64d
        }).ToArray();
        _bars = _close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var expectedNative = NativeSingle();
        var parallel = NativeParallel();
        for (int i = 0; i < Count - (IsSma ? 19 : 0); i++)
        {
            if (Case == "SmaDecimal")
            {
                if (!double.IsFinite(parallel[i]) || Math.Abs(expectedNative[i] - parallel[i]) > 1e-10)
                    throw new InvalidOperationException("Native parallel decimal mismatch.");
            }
            else if (BitConverter.DoubleToInt64Bits(expectedNative[i]) != BitConverter.DoubleToInt64Bits(parallel[i]))
                throw new InvalidOperationException("Native parallel mismatch.");
        }
        var expected = new double[Count];
        if (IsSma) CpuFeasibilityPrototypes.CurrentSma(_close, expected, 20);
        else for (int i = 0; i < Count; i++) expected[i] = Math.Asin(_close[i]);
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            IIndicator indicator = IsSma ? new Sma(20) : new PriceCircularTransform(PriceCircularOperation.ArcSine);
            using var actual = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, actual[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;

    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureHistory(history).BuildAsync().GetAwaiter().GetResult();

    private IIndicatorRun Build(IndicatorHistoryMode history) => Build(IsSma ? new Sma(20)
        : new PriceCircularTransform(PriceCircularOperation.ArcSine), history);

    [Benchmark] public IIndicatorRun BuilderFull() => Build(IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(IndicatorHistoryMode.LatestOnly);

    [Benchmark(Baseline = true)]
    public double[] NativeSingle()
    {
        var output = new double[Count];
        if (IsSma) Functions.Sma<double>(_close, System.Range.All, output, out _, 20);
        else Functions.Asin<double>(_close, System.Range.All, output, out _);
        return output;
    }

    [Benchmark]
    public double[] NativeParallel()
    {
        var output = new double[Count];
        int chunks = Math.Min(Workers, Environment.ProcessorCount);
        int defined = Count - (IsSma ? 19 : 0);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = (int)((long)defined * chunk / chunks);
            int end = (int)((long)defined * (chunk + 1) / chunks);
            if (IsSma) Functions.Sma<double>(_close.AsSpan(start, end - start + 19), System.Range.All,
                output.AsSpan(start, end - start), out _, 20);
            else Functions.Asin<double>(_close.AsSpan(start, end - start), System.Range.All,
                output.AsSpan(start, end - start), out _);
        });
        return output;
    }
}
