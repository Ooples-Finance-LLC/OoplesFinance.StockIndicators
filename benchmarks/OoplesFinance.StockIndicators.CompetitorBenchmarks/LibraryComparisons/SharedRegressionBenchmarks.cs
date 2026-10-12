using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Native returns packed full-window outputs. Builder additionally computes its
// available-history startup and Full retains owned bars. No adapter normalization
// is timed. Exact builder arithmetic and native floating moments differ in rounding.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRegressionBenchmarks
{
    private const int Period = 20;
    [Params("LinearReg", "LinearRegSlope", "LinearRegIntercept", "LinearRegAngle", "Tsf")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!;
    private int _previousDop;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        (_closes, _bars) = PackedWindowBenchmarkExecution.Input(Count);
        var pair = WindowRegressionComparison.Pairs.Single(p => p.Id == "TaLib.Functions." + Operation);
        var expected = pair.Reference!(CompetitorData.FromCloses(_closes), Period).Outputs["Value"].Values;
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            var actual = run[indicator].ToArray();
            if (actual.Length != expected.Length) throw new InvalidOperationException("Regression output length mismatch.");
            AsinFeasibilityBenchmarks.RequireSame(expected, actual);
        }
        PackedWindowBenchmarkChecks.Verify(Period, NativeSingle(), NativeParallel(),
            () => Create(), Build);
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        Native(_closes, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks;
            int end = output.Length * (chunk + 1) / chunks;
            // Each independent segment starts with its complete lookback window.
            Native(_closes.AsSpan(start, end - start + Period - 1), output.AsSpan(start, end - start));
        });
        return output;
    }
    private void Native(ReadOnlySpan<double> input, Span<double> output)
    {
        System.Range range;
        var status = Operation switch
        {
            "LinearRegSlope" => Functions.LinearRegSlope<double>(input, System.Range.All, output, out range, Period),
            "LinearRegIntercept" => Functions.LinearRegIntercept<double>(input, System.Range.All, output, out range, Period),
            "LinearRegAngle" => Functions.LinearRegAngle<double>(input, System.Range.All, output, out range, Period),
            "Tsf" => Functions.Tsf<double>(input, System.Range.All, output, out range, Period),
            _ => Functions.LinearReg<double>(input, System.Range.All, output, out range, Period)
        };
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period - 1
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native regression output range.");
    }
    internal WindowLinearRegression Create() => new(Period, Operation switch
    {
        "LinearRegSlope" => WindowRegressionOutput.Slope,
        "LinearRegIntercept" => WindowRegressionOutput.Intercept,
        "LinearRegAngle" => WindowRegressionOutput.Angle,
        "Tsf" => WindowRegressionOutput.Forecast,
        _ => WindowRegressionOutput.Endpoint
    });
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode) => Build(indicator, mode, false, false);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode, bool reference = false, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(mode)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("Required GPU regression did not execute on the device.");
        }
        return run;
    }
}
