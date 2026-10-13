using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedVolumeIndexBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Trady.Indicator.PositiveVolumeIndex", "Trady.Indicator.NegativeVolumeIndex")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        // Reuse saved native timings; setup executes only the independent reference.
        var expected = pair.Reference!(data, 20).Outputs["Value"];
        var present = expected.Present ?? expected.Values.Select((v, i) => i >= expected.FirstValid && !double.IsNaN(v)).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = Create();
            using var run = Build(owner, mode);
            AsinFeasibilityBenchmarks.RequireSame(expected.Values.Select((v, i) => present[i] ? v : 0).ToArray(), run[owner.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(), run[owner.IsDefined].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private VolumeConditionedIndex Create() => new(Operation == "Trady.Indicator.PositiveVolumeIndex");
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
