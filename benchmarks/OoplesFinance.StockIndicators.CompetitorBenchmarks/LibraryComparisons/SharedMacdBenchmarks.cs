using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedMacdBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("TaLib.Functions.Macd","TaLib.Functions.MacdFix","TaLib.Functions.MacdExt","Skender.GetMacd","Skender.GetPvo","Trady.Indicator.MovingAverageConvergenceDivergence","Trady.Indicator.MovingAverageConvergenceDivergenceHistogram")]
    public string Operation { get; set; } = "";
    private Bar[] _bars=null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop=CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism=8;
        var pair=ComparisonPairs.Get(Operation);var data=ComparisonVerifier.BenchmarkFixture(pair,Count);_bars=data.IndicatorBars;
        var owner=Create();
        double?[][] expected;
        if(owner is EmaDifferenceSignal ema)
        {
            // Independent grid recurrence also checks columns omitted by the
            // native public result. Unselected owned columns remain absent.
            expected=EmaDifferenceSignalComparison.GridReference(ema.Volume ? data.Volumes : data.Closes,12,26,9,ema.FirstPrice,ema.Percentage);
            for(int slot=0;slot<5;slot++)
                if(((int)ema.Selection & (1<<slot))==0) expected[slot]=new double?[Count];
        }
        else
        {
            var reference=pair.Reference!(data,20);
            expected=pair.OutputNames!.Select(name =>
            {
                var column=reference.Outputs[name];
                return column.Values.Select((v,i) => column.Present![i] ? (double?)v : null).ToArray();
            }).ToArray();
        }
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run=Build(owner,mode);
            for(int slot=0;slot<expected.Length;slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(),run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray(),run[owner.Outputs[slot+expected.Length]].ToArray());
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism=_priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(),IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(),IndicatorHistoryMode.LatestOnly);
    private IIndicator Create()
    {
        if(Operation=="TaLib.Functions.MacdExt") return new ClassicMacd();
        if(Operation.StartsWith("TaLib.",StringComparison.Ordinal)) return new AlignedEmaMacd(fixedCoefficients:Operation.EndsWith("Fix",StringComparison.Ordinal));
        int variant=Array.IndexOf(EmaDifferenceSignalComparison.Ids,Operation);
        return new EmaDifferenceSignal(12,26,9,variant>=2,variant==1,variant==1,EmaDifferenceSignalComparison.Selection(variant));
    }
    private IIndicatorRun Build(IIndicator owner,IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
