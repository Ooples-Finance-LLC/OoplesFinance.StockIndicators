using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedAtrStopBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetAtrStop", "Skender.GetSuperTrend", "Skender.GetChandelier", "Trady.Indicator.ChandelierExit")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = ComparisonVerifier.BenchmarkFixture(ComparisonPairs.Get(Operation), Count).IndicatorBars;
        var owner = Create();
        // Independent exact references only; saved native results are reused.
        var expected = owner is SeededAtrTrailingStop stop
            ? AtrTrailingComparison.GridReference(_bars, 20, 3, stop.Basis)
            : ChandelierComparison.Reference(_bars, 20, 3, Operation == "Skender.GetChandelier", 0);
        if (Operation == "Skender.GetChandelier") Array.Clear(expected[1]);
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
        "Skender.GetAtrStop" => new SeededAtrTrailingStop(20),
        "Skender.GetSuperTrend" => new SeededAtrTrailingStop(20, basis: AtrTrailBasis.Midpoint),
        "Skender.GetChandelier" => new WindowChandelierExit(20, selection: ChandelierExitSelection.Long, zeroFloorHigh: true),
        _ => new WindowChandelierExit(20)
    };
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
