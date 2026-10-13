using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedVariableAverageBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Functions.Mavp")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        // SMA limits2/30 and original variable selector match saved native inputs. Only
        // independent exact references execute here; no native benchmark calls.
        var expected = pair.Reference!(data, 20);
        var names = expected.Outputs.Keys.ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = Create(); using var run = Build(owner, mode);
            for (int slot = 0; slot < names.Length; slot++)
            {
                var column = expected.Outputs[names[slot]];
                var present = column.Present!;
                AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => present[i] ? v : 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(), run[owner.Outputs[slot + names.Length]].ToArray());
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => new VariablePeriodClassicAverage(VariablePeriodComparison.Selection);
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
