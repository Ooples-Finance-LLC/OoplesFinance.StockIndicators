using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSampleShapeBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Skew", "QuanTAlib.Kurtosis")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        (_bars, _input, var data) = QuanWindowBenchmarkInput.Create(Count);
        var pair = SampleShapeComparison.Create(Operation == "QuanTAlib.Kurtosis");
        var expectedColumn = pair.Reference!(data, Period).Outputs["Value"];
        var expected = expectedColumn.Values;
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(native, NativeSingle());
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new WindowSampleShape(Period, Operation == "QuanTAlib.Kurtosis"); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expectedColumn.Present!.Select(v => v ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowSampleShape(Period, Operation == "QuanTAlib.Kurtosis"), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowSampleShape(Period, Operation == "QuanTAlib.Kurtosis"), IndicatorHistoryMode.LatestOnly);
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
        // Native scans its retained price window. All P-1 seed calculations and
        // fresh native state construction remain inside the timed method.
        var state = SampleShapeComparison.NativeIndicator(Period, Operation == "QuanTAlib.Kurtosis");
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_input[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_input[i]).Value;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
