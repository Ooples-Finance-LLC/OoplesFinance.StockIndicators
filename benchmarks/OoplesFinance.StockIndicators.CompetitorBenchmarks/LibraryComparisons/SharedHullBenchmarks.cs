using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedHullBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Hma")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        (_bars, _input, var data) = QuanWindowBenchmarkInput.Create(Count);
        var pair = HullComparison.Create(HullWindowConvention.ExpandingFloor);
        var expectedColumn = pair.Reference!(data, Period).Outputs["Value"];
        var expected = expectedColumn.Values;
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(native, NativeSingle());
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new HullWindow(Period, HullWindowConvention.ExpandingFloor); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expectedColumn.Present!.Select(v => v ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new HullWindow(Period, HullWindowConvention.ExpandingFloor), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new HullWindow(Period, HullWindowConvention.ExpandingFloor), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = new double[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var result = new double[Count];
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk => Calculate(Count * chunk / chunks, Count * (chunk + 1) / chunks, result));
        return result;
    }
    private void Calculate(int start, int end, double[] result)
    {
        // Hma owns three fixed-kernel convolutions. Seed the complete composed
        // lookback; state construction and all seed calculations remain timed.
        var state = new QuanTAlib.Hma(Period);
        for (int i = Math.Max(0, start - Period - (int)Math.Sqrt(Period) + 2); i < start; i++) state.Calc(_input[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_input[i]).Value;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
