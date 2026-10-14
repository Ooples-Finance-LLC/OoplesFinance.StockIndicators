using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRocBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetRoc", "Skender.GetRocWb")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = ComparisonVerifier.BenchmarkFixture(ComparisonPairs.Get(Operation), Count).IndicatorBars;
        var owner = Create();
        // Saved native timings are reused. Independent integer-grid references
        // validate all columns; the native decimal reference differs.
        var expected = Reference(_bars, Operation == "Skender.GetRocWb");
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = Build(owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[slot], run[owner.Outputs[slot]].ToArray());
        }
    }

    private static double[][] Reference(Bar[] bars, bool bands)
    {
        var output = Enumerable.Range(0, bands ? 7 : 5).Select(_ => new double[bars.Length]).ToArray();
        var returns = new double[bars.Length];
        var present = new bool[bars.Length];
        bool emaPossible = true;
        for (int i = 20; i < bars.Length; i++)
        {
            var previous = Units(bars[i - 20].Close);
            var difference = Units(bars[i].Close) - previous;
            present[i] = !previous.IsZero;
            if (present[i]) returns[i] = Round(difference * 100, previous);
            if (!bands)
            {
                output[0][i] = Round(difference, Grid);
                output[1][i] = returns[i];
                output[2][i] = present[i] ? 1 : 0;
                if (i < 39 || Enumerable.Range(i - 19, 20).Any(j => !present[j])) continue;
                var sum = BigInteger.Zero;
                for (int j = i - 19; j <= i; j++) sum += Units(returns[j]);
                output[3][i] = Round(sum, Grid * 20);
                output[4][i] = 1;
                continue;
            }
            output[0][i] = returns[i];
            output[1][i] = present[i] ? 1 : 0;
            emaPossible &= present[i];
            if (i < 22) continue;
            if (emaPossible)
            {
                output[2][i] = i == 22
                    ? Round(Units(returns[20]) + Units(returns[21]) + Units(returns[22]), Grid * 3)
                    : Round(Units(returns[i]) * 2 + Units(output[2][i - 1]) * 2, Grid * 4);
                output[3][i] = 1;
            }
            if (!present[i - 2] || !present[i - 1] || !present[i]) continue;
            var squares = BigInteger.Zero;
            for (int j = i - 2; j <= i; j++) squares += Units(returns[j]) * Units(returns[j]);
            output[4][i] = DispersionReferenceArithmetic.Sqrt(squares, Grid * Grid * 3);
            output[5][i] = -output[4][i];
            output[6][i] = 1;
        }
        return output;
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation == "Skender.GetRoc" ? new RateOfChangeWithAverage(20, 20) : new RateOfChangeRmsBands(20, 3, 3);
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
