using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedExpandingRecurrenceBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Rema", "QuanTAlib.Zlema")] public string Operation { get; set; } = "QuanTAlib.Rema";
    private bool Regularized => Operation == "QuanTAlib.Rema";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation is not ("QuanTAlib.Rema" or "QuanTAlib.Zlema")) throw new ArgumentException("Unknown expanding recurrence operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 1, p - 1, p, 1)).ToArray();
        _input = prices.Select((p, i) => new QuanTAlib.TValue(_bars[i].Time, p, true, false)).ToArray();
        var expected = ExpandingRecurrenceComparison.Reference(prices, Period, Regularized, .5, false);
        var nativeExpected = ExpandingRecurrenceComparison.Reference(prices, Period, Regularized, .5, true);
        var serial = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(nativeExpected, serial);
        AsinFeasibilityBenchmarks.RequireSame(serial, NativeParallel());
        for (int i = 0; i < Count; i++)
            if (Math.Abs(serial[i] - expected[i]) > 1e-9)
                throw new InvalidOperationException("Native and owned expanding recurrence disagree on benchmark inputs.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
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
        QuanTAlib.AbstractBase state = Regularized ? new QuanTAlib.Rema(Period, .5) : new QuanTAlib.Zlema(Period);
        // The prior published output is recursive. Exact worker outputs require
        // replay from the beginning; all prefix work and allocations are measured.
        int seed = 0;
        for (int i = seed; i < start; i++) state.Calc(_input[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_input[i]).Value;
    }
    private IIndicator Create() => Regularized ? new ExpandingRegularizedEma(Period, .5) : new ExpandingZeroLagEma(Period);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
