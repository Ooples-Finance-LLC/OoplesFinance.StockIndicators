using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare generated ZLEMA/TMA/HMA against their own legacy route, preserving
// each staged averaging operation. No native startup convention is substituted.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingComposedAverageBenchmarks
{
    [Params("Zlema", "Tma", "Hma")]
    public string Operation { get; set; } = "Zlema";
    [Params(1_000, 10_000)] public int Count { get; set; }
    [Params(IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly)]
    public IndicatorHistoryMode History { get; set; }
    private Bar[] _bars = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double price = 10 + (i * 13 % 101) / 8d;
            return new Bar(default, price, price + 1, price - 2, price, i % 7 + 1);
        }).ToArray();
        var indicator = Create();
        using var expected = Build(indicator, legacy: true);
        using var actual = Build(indicator, legacy: false);
        var output = indicator.Outputs[0];
        var reference = expected[output].ToArray();
        var candidate = actual[output].ToArray();
        if (reference.Length != Count || candidate.Length != Count)
            throw new InvalidOperationException("Composed average benchmark output length mismatch.");
        for (int i = 0; i < Count; i++)
            if (BitConverter.DoubleToInt64Bits(reference[i]) != BitConverter.DoubleToInt64Bits(candidate[i]))
                throw new InvalidOperationException($"Composed average benchmark output mismatch at {i}.");
    }

    // Include identical construction, execution, reading and disposal in both arms.
    [Benchmark(Baseline = true)] public double Legacy() => Run(legacy: true);
    [Benchmark] public double Shared() => Run(legacy: false);

    private double Run(bool legacy)
    {
        var indicator = Create();
        using var run = Build(indicator, legacy);
        return run[indicator.Outputs[0]][Count - 1];
    }

    private IIndicator Create() => Operation switch
    {
        "Zlema" => new Zlema(20),
        "Tma" => new Tma(20),
        "Hma" => new Hma(20),
        _ => throw new ArgumentOutOfRangeException(nameof(Operation))
    };

    private IIndicatorRun Build(IIndicator indicator, bool legacy)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(indicator).ConfigureHistory(History)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);
        if (legacy) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
