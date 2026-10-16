using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedHilbertSignalBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Functions.HtDcPhase", "TaLib.Functions.HtSine", "TaLib.Functions.HtTrendMode", "TaLib.Functions.HtTrendline")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        // Default suppression0 matches saved native inputs. Only
        // independent exact references execute here; no native benchmark calls.
        var expected = pair.Reference!(data, 20);
        var names = pair.OutputNames!;
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = Create(); using var run = Build(owner, mode);
            run.Dispose();
            for (int slot = 0; slot < names.Length; slot++)
            {
                var column = expected.Outputs[names[slot]];
                var present = column.Present!;
                AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => present[i] ? v : 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(), run[owner.Outputs[slot + names.Length]].ToArray());
                if (Count > 0)
                {
                    AsinFeasibilityBenchmarks.RequireSame([present[^1] ? column.Values[^1] : 0], [run.Latest[owner.Outputs[slot]]]);
                    AsinFeasibilityBenchmarks.RequireSame([present[^1] ? 1d : 0d], [run.Latest[owner.Outputs[slot + names.Length]]]);
                }
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation switch
    {
        "TaLib.Functions.HtDcPhase" => new DelayedHilbertPhase(),
        "TaLib.Functions.HtSine" => new DelayedHilbertSine(),
        "TaLib.Functions.HtTrendMode" => new DelayedHilbertTrendMode(),
        "TaLib.Functions.HtTrendline" => new DelayedHilbertTrendline(),
        _ => throw new ArgumentException("Unknown Hilbert signal.")
    };
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
