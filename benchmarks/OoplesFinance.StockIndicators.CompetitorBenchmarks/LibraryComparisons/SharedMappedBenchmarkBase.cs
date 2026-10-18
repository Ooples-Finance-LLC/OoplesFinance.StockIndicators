using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>Builder-only harness for mappings that reuse saved native timings.</summary>
public abstract class SharedMappedBenchmarkBase
{
    [Params(1000, 10000)] public int Count { get; set; }
    protected Bar[] _bars = null!;
    private int _priorDop;

    private protected (ComparisonPair Pair, CompetitorData Data) PrepareFixture(string operation)
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var pair = ComparisonPairs.Get(operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _bars = data.IndicatorBars;
        return (pair, data);
    }

    protected abstract IIndicator Create();
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => SharedBenchmarkSupport.BuildCpu(_bars, Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => SharedBenchmarkSupport.BuildCpu(_bars, Create(), IndicatorHistoryMode.LatestOnly);
}
