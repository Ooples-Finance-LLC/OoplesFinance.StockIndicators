using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>Complete public builder lifecycle versus native competitor public APIs.</summary>
[MemoryDiagnoser]
[Config(typeof(CpuBuilderTimingConfig))]
public class CpuBuilderBenchmarks
{
    public static IEnumerable<string> Cases => Environment.GetEnvironmentVariable("COMPARISON_PAIR") is { } id
        ? CpuBuilderWorkload.PairIds.Where(value => value == id)
        : Environment.GetEnvironmentVariable("COMPARISON_FAMILY") is { } family
            ? CpuBuilderWorkload.FamilyPairs(family) : CpuBuilderWorkload.PairIds;
    [ParamsSource(nameof(Cases))] public string PairId { get; set; } = "";
    [Params(1_000, 10_000)] public int Bars { get; set; }
    private CpuNativeWorkload _work = null!;
    [GlobalSetup]
    public void Setup()
    {
        _work = new CpuNativeWorkload(PairId, Bars, commonGrid: true);
        // Verify the actual complete builder output before measuring it.
        CpuBuilderWorkload.Verify(_work);
        if (PairId is "Skender.GetSma" or "Skender.GetSma.Tuple" or "TaLib.Functions.Sma" or "QuanTAlib.Sma")
        {
            var pair = ComparisonPairs.Get(CpuNativeWorkload.CanonicalPair(PairId));
            ComparisonVerifier.Compare(pair.Competitor(_work.Data, 20), _work.Normalize(_work.NativeOwned()),
                PairId + " complete native output", pair.ErrorBudget);
        }
    }
    [Benchmark] public async Task<int> OoplesBuilderBatch()
    {
        using var run = await CpuBuilderWorkload.Build(_work);
        // BuildAsync materializes every output. Do not add a second copy solely
        // for the benchmark: the run already owns the complete result arrays.
        return run.BarCount;
    }
    [Benchmark] public object CompetitorNativeBatch() => _work.NativeOwned();
}

public sealed class CpuBuilderTimingConfig : ManualConfig
{
    public CpuBuilderTimingConfig()
    {
        var slow = (Environment.GetEnvironmentVariable("COMPARISON_PAIR")
            ?? Environment.GetEnvironmentVariable("COMPARISON_FAMILY")) is null
            or "Trady.Candlestick.BullishShortDay" or "Trady.Candlestick.BullishShortDay.Tuple";
        var job = Job.ShortRun.WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(30), true))
            .WithUnrollFactor(1).WithWarmupCount(slow ? 3 : 8).WithIterationCount(slow ? 3 : 5);
        AddJob(slow ? job.WithInvocationCount(1)
            : job.WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100)));
    }
}

internal static class CpuBuilderWorkload
{
    internal static readonly string[] PairIds = [.. CpuKernelPilots.Ids,
        "Skender.GetSma", "Skender.GetSma.Tuple", "TaLib.Functions.Sma", "QuanTAlib.Sma",
        "Trady.Indicator.SimpleMovingAverage.Tuple", "Trady.Candlestick.BullishShortDay.Tuple"];
    internal static IEnumerable<string> FamilyPairs(string family) => family switch
    {
        "Trady.Indicator.SimpleMovingAverage" => PairIds.Where(id => id.Contains("SimpleMovingAverage", StringComparison.Ordinal)
            || id.Contains(".Sma", StringComparison.Ordinal) || id.Contains(".GetSma", StringComparison.Ordinal)),
        "Trady.Candlestick.BullishShortDay" => PairIds.Where(id => id.StartsWith(family, StringComparison.Ordinal)),
        _ when CpuKernelPilots.Ids.Contains(family) => [family],
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };
    internal static IIndicator Create(string pair) => CpuNativeWorkload.CanonicalPair(pair) switch
    {
        "QuanTAlib.Jma" => new JurikAdaptive(20, 0, 10),
        "QuanTAlib.Atr" => new ScaledTrueRange(20),
        "Skender.GetRollingPivots" => new RollingPivotLevels(20),
        "Skender.GetFractal" => new RetrospectiveFractals(20, 20),
        "TaLib.Candles.RickshawMan" => new RickshawManCandle(10, 5),
        "TaLib.Functions.Asin" => new PriceCircularTransform(PriceCircularOperation.ArcSine),
        "Trady.Candlestick.BullishShortDay" => new BullishShortBodyCandle(20, .25m),
        "Trady.Indicator.SimpleMovingAverage" or "Skender.GetSma" or "Skender.GetSma.Tuple"
            or "TaLib.Functions.Sma" or "QuanTAlib.Sma" => new Sma(20),
        _ => throw new ArgumentOutOfRangeException(nameof(pair))
    };
    internal static Task<IIndicatorRun> Build(CpuNativeWorkload work) => Build(work, Create(work.PairId));
    internal static Task<IIndicatorRun> Build(CpuNativeWorkload work, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(work.Data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync();

    internal static void Verify(CpuNativeWorkload work)
    {
        var indicator = Create(work.PairId);
        using var run = Build(work, indicator).GetAwaiter().GetResult();
        var pair = ComparisonPairs.Get(CpuNativeWorkload.CanonicalPair(work.PairId));
        var expected = pair.Ooples(work.Data, 20);
        var names = pair.OutputNames ?? ["Value"];
        if (run.BarCount != work.Data.Count) throw new InvalidOperationException("Builder truncated its output.");
        for (var slot = 0; slot < names.Length; slot++)
        {
            var values = run[indicator.Outputs[slot]];
            var presentSlot = indicator switch
            {
                RollingPivotLevels => slot + 9,
                RetrospectiveFractals => slot + 2,
                PriceCircularTransform => 1,
                _ => -1
            };
            var expectedOutput = expected.Outputs[names[slot]];
            for (var i = 0; i < values.Length; i++)
            {
                var present = presentSlot < 0 || run[indicator.Outputs[presentSlot]][i].Equals(1d);
                var expectedPresent = expectedOutput.Present?[i] ?? !double.IsNaN(expectedOutput.Values[i]);
                // SMA's warmup is finite zero in our public builder contract.
                if (indicator is Sma && i < 19)
                {
                    if (!values[i].Equals(0d)) throw new InvalidOperationException("Unexpected SMA startup.");
                    continue;
                }
                if (present != expectedPresent || (present && !values[i].Equals(expectedOutput.Values[i]))
                    || (!present && !values[i].Equals(0d)))
                    throw new InvalidOperationException($"{work.PairId}: builder output {slot}, bar {i} differs from its public reference.");
            }
        }
    }
}
