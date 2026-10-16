using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedFibonacciWeightedBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Fwma")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        var values = FixedKernelBenchmarkInput.Create(Count, out _bars, out _input, out _priorDop);
        var pair = FixedKernelSnapshotComparison.Pair(true);
        var data = CompetitorData.FromCloses(values);
        var expected = pair.Reference!(data, Period).Outputs["Value"].Values;
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(native, NativeSingle());
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new FibonacciWeightedAverage(Period);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new FibonacciWeightedAverage(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new FibonacciWeightedAverage(Period), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) =>
        SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);

    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = new double[Count]; Calculate(0, Count, output); return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = new double[Count];
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks,
            chunk => Calculate(Count * chunk / chunks, Count * (chunk + 1) / chunks, output));
        return output;
    }
    private void Calculate(int start, int end, double[] output)
    {
        var state = new QuanTAlib.Fwma(Period);
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_input[i]);
        for (int i = start; i < end; i++) output[i] = state.Calc(_input[i]).Value;
    }
}
