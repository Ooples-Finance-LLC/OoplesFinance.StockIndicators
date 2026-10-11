using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedWeightedWindowBenchmarks
{
    private const int Period = 20;
    [Params("Wma", "Convolution", "Gma", "Sinema", "Epma", "Mma")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _inputs = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var close = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _bars = close.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        _inputs = close.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        var data = CompetitorData.FromCloses(close);
        var pair = FixedWeightedComparison.Pairs.Concat(ConvolutionComparison.Pairs).Concat(AnalyticKernelComparison.Pairs).Append(MeanStartupEndpointComparison.Pair)
            .Single(p => p.Id == "QuanTAlib." + Operation);
        // The published contracts have different rounding boundaries. Validate
        // each against its independent arithmetic oracle, including startup.
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(pair.CompetitorReference!(data, Period).Outputs["Value"].Values, native);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        var exact = pair.Reference!(data, Period).Outputs["Value"].Values;
        SharedWindowBenchmarkValidation.Cpu(exact, Create, (indicator, history, reference) => Build(indicator, history, reference));
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        Native(0, Count, result);
        return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            Native(start, end, result);
        });
        return result;
    }
    private void Native(int start, int end, double[] result)
    {
        // QuanTAlib 1.0.0 exposes Calc(TValue), with fixed-kernel convolution.
        // Replay only P-1 inputs and write disjoint outputs without adapters.
        var state = CreateNative();
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_inputs[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_inputs[i]).Value;
    }
    private QuanTAlib.AbstractBase CreateNative() => Operation switch
    {
        "Epma" => new QuanTAlib.Epma(Period),
        "Mma" => new QuanTAlib.Mma(Period),
        "Wma" => new QuanTAlib.Wma(Period),
        "Convolution" => new QuanTAlib.Convolution(Kernel()),
        "Gma" => new QuanTAlib.Gma(Period),
        "Sinema" => new QuanTAlib.Sinema(Period),
        _ => throw new InvalidOperationException("Unknown weighted-window operation.")
    };
    private IIndicator Create() => Operation switch
    {
        "Epma" => new EndpointWeightedAverage(Period),
        "Mma" => new EndpointWeightedAverage(Period, true),
        "Wma" => new FixedPeriodWma(Period),
        "Convolution" => new NormalizedConvolution(Kernel()),
        "Gma" => new GaussianWeightedAverage(Period),
        "Sinema" => new SineWeightedAverage(Period),
        _ => throw new InvalidOperationException("Unknown weighted-window operation.")
    };
    private static double[] Kernel() => Enumerable.Range(1, Period).Select(i => (double)i).ToArray();
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
