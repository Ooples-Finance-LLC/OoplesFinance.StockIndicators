using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedLaggedChangeBenchmarks
{
    private const int Period = 20;
    [Params("Mom", "RocP", "Roc", "RocR", "RocR100")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _close = null!;
    private int _previousDop;
    private PriceChangeKind _kind;
    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _kind = PriceChangeComparison.Forms.Single(f => f.Name == Operation).Kind;
        _close = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _bars = _close.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        AsinFeasibilityBenchmarks.RequireSame(NativeSingle(), NativeParallel());
        var indicator = Create();
        using var ordinary = Build(indicator, IndicatorHistoryMode.Full, true);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var result = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(ordinary[indicator].ToArray(), result[indicator].ToArray());
        }
        // Rate forms have distinct rounding boundaries in the two libraries.
        // Compare each execution strategy to its own complete serial contract.
        if (Operation == "Mom") AsinFeasibilityBenchmarks.RequireSame(NativeSingle(), ordinary[indicator].Slice(Period).ToArray());
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period);
        Native(_close, output); return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
            Native(_close.AsSpan(start, end - start + Period), output.AsSpan(start, end - start));
        });
        return output;
    }
    private void Native(ReadOnlySpan<double> close, Span<double> output)
    {
        System.Range range;
        var status = Operation switch
        {
            "Mom" => Functions.Mom<double>(close, System.Range.All, output, out range, Period),
            "RocP" => Functions.RocP<double>(close, System.Range.All, output, out range, Period),
            "Roc" => Functions.Roc<double>(close, System.Range.All, output, out range, Period),
            "RocR" => Functions.RocR<double>(close, System.Range.All, output, out range, Period),
            "RocR100" => Functions.RocR100<double>(close, System.Range.All, output, out range, Period),
            _ => throw new InvalidOperationException("Unknown lagged change operation.")
        };
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native lagged output range.");
    }
    private LaggedPriceChange Create() => new(Period, _kind);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
