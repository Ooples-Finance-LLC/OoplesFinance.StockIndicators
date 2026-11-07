using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingSteppedChannelBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("EfficientTrendStepChannel", "LinearChannels")]
    public string Operation { get; set; } = "EfficientTrendStepChannel";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "EfficientTrendStepChannel" => new EfficientTrendStepChannel(),
        "LinearChannels" => new LinearChannels(),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
