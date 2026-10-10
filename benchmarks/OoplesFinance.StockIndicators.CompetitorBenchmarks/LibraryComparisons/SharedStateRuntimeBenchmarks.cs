using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Isolates legacy-runtime overhead with identical state arithmetic and Full history.
// This is an internal before/after comparison, not native-competitor qualification.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedStateRuntimeBenchmarks
{
    public static IEnumerable<string> Cases => new[] { "Ema", "Sum", "Convolution", "Regression", "Dispersion" }
        .Where(name => Environment.GetEnvironmentVariable("SHARED_STATE") is not { } selected
            || selected.Split(',').Contains(name, StringComparer.Ordinal));
    [ParamsSource(nameof(Cases))]
    public string Family { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double value = 10 + (i % 101) / 8d;
            return new Bar(default, value, value + 1, value - 1, value, 1);
        }).ToArray();
        var indicator = Create();
        using var direct = Build(indicator, false);
        using var legacy = Build(indicator, true);
        foreach (var output in indicator.Outputs)
            AsinFeasibilityBenchmarks.RequireSame(legacy[output].ToArray(), direct[output].ToArray());
    }

    [Benchmark(Baseline = true)] public IIndicatorRun LegacyRuntime() => Build(Create(), true);
    [Benchmark] public IIndicatorRun StateGraph() => Build(Create(), false);

    private IIndicatorRun Build(IIndicator indicator, bool legacy)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator);
        if (legacy) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }

    private IIndicator Create() => Family switch
    {
        "Ema" => new FirstValueEma(20),
        "Sum" => new RollingPriceSum(20),
        "Convolution" => new NormalizedConvolution(new[] { 1d, 2d, 3d, 4d }),
        "Regression" => new WindowLinearRegression(20),
        "Dispersion" => new WindowDispersion(20),
        _ => throw new ArgumentOutOfRangeException(nameof(Family))
    };
}
