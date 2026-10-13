using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRegressionChannelBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetStdDevChannels")] public string Operation { get; set; } = "Skender.GetStdDevChannels";
    private Bar[] _bars = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        var expected = pair.Reference!(data, 20);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = new RegressionWithChannels();
            using var run = Build(owner, history);
            run.Dispose();
            for (var slot = 0; slot < 4; slot++)
            {
                var column = expected.Outputs[pair.OutputNames![slot]];
                var values = column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray();
                AsinFeasibilityBenchmarks.RequireSame(values, run[owner.Outputs[slot]].ToArray());
                if (Count > 0) AsinFeasibilityBenchmarks.RequireSame([values[^1]], [run.Latest[owner.Outputs[slot]]]);
                if (slot == 3) continue;
                var mask = column.Present!.Select(p => p ? 1d : 0d).ToArray();
                AsinFeasibilityBenchmarks.RequireSame(mask, run[owner.Outputs[slot + 4]].ToArray());
                if (Count > 0) AsinFeasibilityBenchmarks.RequireSame([mask[^1]], [run.Latest[owner.Outputs[slot + 4]]]);
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    // Original period20/deviations2, saved native input generator. No native calls.
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new RegressionWithChannels(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new RegressionWithChannels(), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(RegressionWithChannels owner, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
