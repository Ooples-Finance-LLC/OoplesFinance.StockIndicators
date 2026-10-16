using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Compare each generated price-average alias with its own legacy route.
// Distinct OHLC fields prevent an accidental field substitution from passing setup.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class RemainingPriceAverageBenchmarks
{
    [Params("AveragePrice", "MidRange", "HlcAverage", "OhlcAverage")]
    public string Operation { get; set; } = "AveragePrice";
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
            return new Bar(default, price - .75, price + 1, price - 2, price, i % 7 + 1);
        }).ToArray();
        var indicator = Create();
        using var expected = Build(indicator, legacy: true);
        using var actual = Build(indicator, legacy: false);
        var output = indicator.Outputs[0];
        var reference = expected[output].ToArray();
        var candidate = actual[output].ToArray();
        if (reference.Length != Count || candidate.Length != Count)
            throw new InvalidOperationException("Price average benchmark output length mismatch.");
        for (int i = 0; i < Count; i++)
            if (BitConverter.DoubleToInt64Bits(reference[i]) != BitConverter.DoubleToInt64Bits(candidate[i]))
                throw new InvalidOperationException($"Price average benchmark output mismatch at {i}.");
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
        "AveragePrice" => new AveragePrice(20),
        "MidRange" => new MidRange(),
        "HlcAverage" => new HlcAverage(),
        "OhlcAverage" => new OhlcAverage(),
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
