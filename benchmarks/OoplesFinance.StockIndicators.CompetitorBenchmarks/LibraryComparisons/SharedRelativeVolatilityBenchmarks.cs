using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRelativeVolatilityBenchmarks
{
    private const int Period = 14;
    [Params("QuanTAlib.Rvi")] public string Operation { get; set; } = "QuanTAlib.Rvi";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation != "QuanTAlib.Rvi") throw new ArgumentException("Unknown relative volatility operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 1, p - 1, p, 1)).ToArray();
        _input = prices.Select((p, i) => new QuanTAlib.TValue(_bars[i].Time, p, true, false)).ToArray();
        var expected = RelativeVolatilityComparison.Reference(prices, Period);
        var nativeExpected = RelativeVolatilityComparison.NativeReference(prices, Period);
        var serial = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(nativeExpected, serial);
        AsinFeasibilityBenchmarks.RequireSame(serial, NativeParallel());
        for (int i = 0; i < Count; i++)
            if (Math.Abs(serial[i] - expected[i]) > 1e-9)
                throw new InvalidOperationException("Native and owned relative volatility disagree on benchmark inputs.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new RelativeVolatilityStrength(Period);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new RelativeVolatilityStrength(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new RelativeVolatilityStrength(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = new double[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var result = new double[Count];
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, double[] result)
    {
        var state = new QuanTAlib.Rvi(Period);
        // P smoothed deviations each need P changes; the oldest change also
        // needs its preceding price. The synthetic first change expires before
        // the first assigned output. All seeding and native allocations are timed.
        int seed = Math.Max(0, start - (2 * Period - 1));
        for (int i = seed; i < start; i++) state.Calc(_input[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_input[i]).Value;
    }
    private IIndicatorRun Build(RelativeVolatilityStrength indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
