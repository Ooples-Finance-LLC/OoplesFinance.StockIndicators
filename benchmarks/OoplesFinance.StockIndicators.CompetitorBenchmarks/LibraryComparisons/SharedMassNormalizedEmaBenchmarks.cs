using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Separate constructor contracts; these results must not replace seeded EMA rows.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedMassNormalizedEmaBenchmarks
{
    private const int Period = 20;
    private const double ExplicitAlpha = .2;
    [Params(false, true)] public bool ExplicitWeight { get; set; }
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _inputs = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var input = SkenderWindowBenchmarkInput.Create(Count, 0);
        _bars = input.Bars;
        _inputs = input.Prices.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        double alpha = ExplicitWeight ? ExplicitAlpha : 2d / (Period + 1);
        var exact = MassNormalizedComparison.Reference(input.Prices, [alpha], false);
        var native = MassNormalizedComparison.Reference(input.Prices, [alpha], true);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeSingle());
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(exact, run[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    private MassNormalizedEma Create() => ExplicitWeight
        ? new MassNormalizedEma(ExplicitAlpha) : new MassNormalizedEma(Period);
    [Benchmark] public IIndicatorRun BuilderFull() =>
        SharedBenchmarkSupport.BuildCpu(_bars, Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() =>
        SharedBenchmarkSupport.BuildCpu(_bars, Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        Calculate(0, Count, result);
        return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        int chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            Calculate(Count * chunk / chunks, Count * (chunk + 1) / chunks, result));
        return result;
    }
    private void Calculate(int start, int end, double[] result)
    {
        var state = ExplicitWeight ? new QuanTAlib.Ema(ExplicitAlpha) : new QuanTAlib.Ema(Period, false);
        // Each independent recursive partition must replay its complete prefix.
        for (int i = 0; i < start; i++) state.Calc(_inputs[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_inputs[i]).Value;
    }
}
