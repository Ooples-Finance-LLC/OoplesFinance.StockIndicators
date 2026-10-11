using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedLogVolatilityBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Historical", "QuanTAlib.Realized")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism; CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var values = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _bars = values.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        _input = values.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        var data = CompetitorData.FromCloses(values);
        var pair = LogVolatilityComparison.Pair(Operation == "QuanTAlib.Realized");
        var expectedColumn = pair.Reference!(data, Period).Outputs["Value"];
        var expected = expectedColumn.Values;
        var native = pair.CompetitorReference!(data, Period).Outputs["Value"].Values;
        AsinFeasibilityBenchmarks.RequireSame(native, NativeSingle());
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new WindowLogVolatility(Period, Operation == "QuanTAlib.Realized"); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expectedColumn.Present!.Select(v => v ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowLogVolatility(Period, Operation == "QuanTAlib.Realized"), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowLogVolatility(Period, Operation == "QuanTAlib.Realized"), IndicatorHistoryMode.LatestOnly);
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
        // Historical can replay P returns from P+1 strictly positive prices in
        // this fixture. Realized's floating rolling sum depends on all prior
        // arithmetic, so each worker replays the full prefix; all replay is timed.
        var state = LogVolatilityComparison.Indicator(Period, Operation == "QuanTAlib.Realized", true);
        for (int i = Operation == "QuanTAlib.Realized" ? 0 : Math.Max(0, start - Period); i < start; i++) state.Calc(_input[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_input[i]).Value;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
