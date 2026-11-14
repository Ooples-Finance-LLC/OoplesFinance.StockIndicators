using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Qualify every additional facade independently against its legacy route.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingRegressionBenchmarks : RemainingIndicatorBenchmarkBase
{
    [Params("Lsma", "LinRegSlope", "LinearRegressionSlope", "LinRegIntercept", "LinearRegressionIntercept", "LinearRegressionLine", "LinReg", "LinearChannelMiddle")]
    public string Operation { get; set; } = "Lsma";

    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    protected override IIndicator Create() => Operation switch
    {
        "Lsma" => new Lsma(20),
        "LinRegSlope" => new LinRegSlope(20),
        "LinearRegressionSlope" => new LinearRegressionSlope(20),
        "LinRegIntercept" => new LinRegIntercept(20),
        "LinearRegressionIntercept" => new LinearRegressionIntercept(20),
        "LinearRegressionLine" => new LinearRegressionLine(20),
        "LinReg" => new LinReg(20),
        "LinearChannelMiddle" => new LinearChannelMiddle(20),
        _ => throw new InvalidOperationException($"Unknown benchmark operation: {Operation}.")
    };
}
