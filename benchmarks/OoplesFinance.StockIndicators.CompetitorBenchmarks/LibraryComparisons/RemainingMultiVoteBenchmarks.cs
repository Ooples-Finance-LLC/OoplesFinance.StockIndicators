using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingMultiVoteBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("MultiVoteOnBalanceVolume")]
    public string Operation { get; set; } = "MultiVoteOnBalanceVolume";
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);
    protected override IIndicator Create() => Operation switch
    {
        "MultiVoteOnBalanceVolume" => new MultiVoteOnBalanceVolume(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
