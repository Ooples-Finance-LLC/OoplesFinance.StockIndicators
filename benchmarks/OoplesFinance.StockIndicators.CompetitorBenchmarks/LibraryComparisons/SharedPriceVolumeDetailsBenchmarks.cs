using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPriceVolumeDetailsBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetBop", "Skender.GetElderRay", "Trady.Indicator.AccumulationDistributionLine")]
    public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        var owner = Create();
        double[][] expected;
        if (owner is BalanceOfPowerWithValidity)
        {
            expected = [new double[Count], new double[Count]];
            var raw = _bars.Select(b => b.High == b.Low ? (double?)null : Round(Units(b.Close) - Units(b.Open), Units(b.High) - Units(b.Low))).ToArray(); // NOSONAR: S1244 - Exactly flat candles are absent in this reference convention.
            for (int i = 19; i < Count; i++)
            {
                if (Enumerable.Range(i - 19, 20).Any(j => !raw[j].HasValue)) continue;
                var sum = BigInteger.Zero;
                for (int j = i - 19; j <= i; j++) sum += Units(raw[j]!.Value);
                expected[0][i] = Round(sum, Grid * 20); expected[1][i] = 1;
            }
        }
        else
        {
            var reference = pair.Reference!(data, 20);
            var names = pair.OutputNames ?? ["Value"];
            expected = Enumerable.Range(0, owner.Outputs.Count).Select(_ => new double[Count]).ToArray();
            for (int slot = 0; slot < names.Length; slot++)
            {
                var column = reference.Outputs[names[slot]];
                var present = column.Present ?? column.Values.Select((v, i) => i >= column.FirstValid && !double.IsNaN(v)).ToArray();
                expected[slot] = column.Values.Select((v, i) => present[i] ? v : 0).ToArray();
                expected[^1] = present.Select(v => v ? 1d : 0d).ToArray();
            }
        }
        // No native implementations run here; the references above are independent.
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = Build(owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[slot], run[owner.Outputs[slot]].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation switch
    {
        "Skender.GetBop" => new BalanceOfPowerWithValidity(20),
        "Skender.GetElderRay" => new ElderRayWithDetails(20),
        _ => new PriceAdjustedAccumulationDistribution()
    };
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
