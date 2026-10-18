using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser,Config(typeof(TensorsGpuTimingConfig))]
public class SharedEnvelopeBenchmarks
{
    [Params(1000,10000)] public int Count { get; set; }
    [Params("Skender.GetMaEnvelopes","Skender.GetKeltner","Skender.GetStarcBands","Trady.Indicator.KeltnerChannels")]
    public string Operation { get; set; } = "";
    private Bar[] _bars=null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop=CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism=8;
        var pair=ComparisonPairs.Get(Operation);
        var data=ComparisonVerifier.BenchmarkFixture(pair,Count);
        _bars=data.IndicatorBars;
        // Independent references validate outputs without native benchmark calls.
        var expected=pair.Reference!(data,20);
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner=Create();using var run=Build(owner,mode);
            var names=pair.OutputNames!;
            for(int slot=0;slot<names.Length;slot++)
            {
                var reference=expected.Outputs[names[slot]];
                var present=reference.Present!;
                AsinFeasibilityBenchmarks.RequireSame(reference.Values.Select((v,i) => present[i] ? v : 0).ToArray(),run[owner.Outputs[slot]].ToArray());
                int flag=owner is WindowAverageEnvelope ? 3 : slot+4;
                AsinFeasibilityBenchmarks.RequireSame(present.Select(p => p ? 1d : 0d).ToArray(),run[owner.Outputs[flag]].ToArray());
            }
            if(owner is WindowAtrBands { IncludeWidth:false })
            {
                AsinFeasibilityBenchmarks.RequireSame(new double[Count],run[owner.Outputs[3]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(new double[Count],run[owner.Outputs[7]].ToArray());
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism=_priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(),IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(),IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation switch
    {
        "Skender.GetMaEnvelopes" => new WindowAverageEnvelope(20),
        "Skender.GetKeltner" => new WindowAtrBands(20,20),
        "Skender.GetStarcBands" => new WindowAtrBands(20,20,2,AtrBandCenterMode.Simple,false),
        "Trady.Indicator.KeltnerChannels" => new WindowAtrBands(20,20,2,AtrBandCenterMode.FirstSeededExponential,false),
        _ => throw new ArgumentException("Unknown envelope operation.")
    };
    private IIndicatorRun Build(IIndicator owner,IndicatorHistoryMode mode) => SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
}
