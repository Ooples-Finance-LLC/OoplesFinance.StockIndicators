using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedAdaptiveVariantBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Functions.Kama", "Trady.Indicator.KaufmanAdaptiveMovingAverage")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private bool _ta;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _ta = Operation switch
        {
            "TaLib.Functions.Kama" => true,
            "Trady.Indicator.KaufmanAdaptiveMovingAverage" => false,
            _ => throw new ArgumentException("Unknown adaptive variant.")
        };
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        // Match saved native inputs: period20, fast2, slow30; TA has fast
        // flat efficiency and one extra startup bar, Trady has slow flat ER.
        // Verify efficiency too, although both native mappings expose only KAMA.
        // This independent exact reference never executes a native method.
        var expected = SeededAdaptiveComparison.ReferenceValues(data.Closes, 20, 2, 30,
            false, false, false, _ta, _ta ? 1 : 0);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = Create();
            using var run = Build(owner, mode);
            run.Dispose();
            for (int slot = 0; slot < 2; slot++)
            {
                var values = expected[slot].Select(v => v ?? 0).ToArray();
                var present = expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray();
                AsinFeasibilityBenchmarks.RequireSame(values, run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(present, run[owner.Outputs[slot + 2]].ToArray());
                if (Count > 0)
                {
                    AsinFeasibilityBenchmarks.RequireSame([values[^1]], [run.Latest[owner.Outputs[slot]]]);
                    AsinFeasibilityBenchmarks.RequireSame([present[^1]], [run.Latest[owner.Outputs[slot + 2]]]);
                }
            }
        }
    }

    private SeededAdaptiveAverage Create() => new(20, resetFlat: false, fastFlat: _ta, outputDelay: _ta ? 1 : 0);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
