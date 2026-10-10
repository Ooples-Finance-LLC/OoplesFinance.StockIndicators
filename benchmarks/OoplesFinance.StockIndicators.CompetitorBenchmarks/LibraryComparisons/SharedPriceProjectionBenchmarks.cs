using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Native methods receive prepared identical columns without normalization adapters.
// BuilderReference forces the ordinary Full evaluator through an empty behavior
// configuration; it is an internal reference path, not a native competitor arm.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPriceProjectionBenchmarks
{
    [Params("AvgPrice", "MedPrice", "TypPrice", "WclPrice")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _open = null!, _high = null!, _low = null!, _close = null!;
    private int _previousDop;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _close = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _open = Enumerable.Range(0, Count).Select(i => 8 + (i * 7 % 97) / 16d).ToArray();
        _high = _close.Select((v, i) => Math.Max(v, _open[i]) + 1).ToArray();
        _low = _close.Select((v, i) => Math.Min(v, _open[i]) - 1).ToArray();
        _bars = _close.Select((v, i) => new Bar(default, _open[i], _high[i], _low[i], v, 1)).ToArray();
        var expected = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var result = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, result[indicator].ToArray());
        }
        var reference = Create();
        using var ordinary = Build(reference, IndicatorHistoryMode.Full, true);
        AsinFeasibilityBenchmarks.RequireSame(expected, ordinary[reference].ToArray());
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count);
        Native(_open, _high, _low, _close, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = Count * chunk / chunks, size = Count * (chunk + 1) / chunks - start;
            Native(_open.AsSpan(start, size), _high.AsSpan(start, size), _low.AsSpan(start, size),
                _close.AsSpan(start, size), output.AsSpan(start, size));
        });
        return output;
    }

    private void Native(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<double> output)
    {
        System.Range range;
        var status = Operation switch
        {
            "AvgPrice" => Functions.AvgPrice<double>(open, high, low, close, System.Range.All, output, out range),
            "MedPrice" => Functions.MedPrice<double>(high, low, System.Range.All, output, out range),
            "TypPrice" => Functions.TypPrice<double>(high, low, close, System.Range.All, output, out range),
            "WclPrice" => Functions.WclPrice<double>(high, low, close, System.Range.All, output, out range),
            _ => throw new InvalidOperationException("Unknown price projection.")
        };
        if (status != TALib.Core.RetCode.Success || range.Start.Value != 0 || range.End.Value != output.Length)
            throw new InvalidOperationException("Unexpected native price projection range.");
    }

    private IIndicator Create() => Operation switch
    {
        "AvgPrice" => new FullTypicalPrice(1),
        "MedPrice" => new MedianPrice(1),
        "TypPrice" => new TypicalPrice(1),
        "WclPrice" => new WeightedClose(1),
        _ => throw new InvalidOperationException("Unknown price projection.")
    };

    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
