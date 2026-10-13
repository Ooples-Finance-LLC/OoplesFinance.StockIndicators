using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSeededMomentumBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetPmo", "Skender.GetTsi", "Skender.GetTrix", "TaLib.Functions.Trix")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var data = ComparisonVerifier.BenchmarkFixture(ComparisonPairs.Get(Operation), Count);
        _bars = data.IndicatorBars;
        var owner = Create();
        // Only independent references execute here; saved native timing is reused.
        var expected = owner is TripleExponentialRate triple
            ? TripleRateComparison.Reference(data.Closes, 20, triple.Seed)
            : SeededMomentumComparison.GridReference(data.Closes, 20, Operation == "Skender.GetPmo" ? 20 : 13,
                Operation == "Skender.GetPmo" ? 10 : 7, Operation == "Skender.GetPmo");
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = Build(owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray(), run[owner.Outputs[slot + expected.Length]].ToArray());
            }
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation switch
    {
        "Skender.GetPmo" => new SeededPriceMomentum(20, 20, 10),
        "Skender.GetTsi" => new SeededTrueStrength(20, 13, 7),
        "Skender.GetTrix" => new TripleExponentialRate(20),
        _ => new TripleExponentialRate(20, TripleRateSeed.CascadedMeans)
    };
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
