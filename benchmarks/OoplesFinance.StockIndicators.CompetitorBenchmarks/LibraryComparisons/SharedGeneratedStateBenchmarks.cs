using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGeneratedStateBenchmarks
{
    private const int Period = 20;
    [Params("Max", "Min", "MinMax", "TRange")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _close = null!, _high = null!, _low = null!;
    private int _previousDop;
    private int Lookback => Operation == "TRange" ? 1 : Period - 1;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _close = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _high = _close.Select(v => Operation == "TRange" ? v + 1 : v).ToArray();
        _low = _close.Select(v => Operation == "TRange" ? v - 1 : v).ToArray();
        _bars = _close.Select((v, i) => new Bar(default, v, _high[i], _low[i], v, 1)).ToArray();
        var expected = Columns(NativeSingle());
        Check(expected, Columns(NativeParallel()));
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicators = Create();
            using var result = Build(indicators, history);
            Check(expected, indicators.Select(i => result[i].Slice(Lookback).ToArray()).ToArray());
        }
        var reference = Create();
        using var ordinary = Build(reference, IndicatorHistoryMode.Full, true);
        Check(expected, reference.Select(i => ordinary[i].Slice(Lookback).ToArray()).ToArray());
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        var first = GC.AllocateUninitializedArray<double>(Count - Lookback);
        var second = Operation == "MinMax" ? GC.AllocateUninitializedArray<double>(Count - Lookback) : Array.Empty<double>();
        Native(_close, _high, _low, first, second);
        return second.Length == 0 ? first : new[] { first, second };
    }
    [Benchmark] public object NativeParallel()
    {
        int lookback = Lookback;
        var first = GC.AllocateUninitializedArray<double>(Count - lookback);
        var second = Operation == "MinMax" ? GC.AllocateUninitializedArray<double>(Count - lookback) : Array.Empty<double>();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = first.Length * chunk / chunks, size = first.Length * (chunk + 1) / chunks - start;
            Native(_close.AsSpan(start, size + lookback), _high.AsSpan(start, size + lookback),
                _low.AsSpan(start, size + lookback), first.AsSpan(start, size), second.Length == 0 ? Span<double>.Empty : second.AsSpan(start, size));
        });
        return second.Length == 0 ? first : new[] { first, second };
    }

    private void Native(ReadOnlySpan<double> close, ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        Span<double> first, Span<double> second)
    {
        System.Range range;
        var status = Operation switch
        {
            "Max" => Functions.Max<double>(close, System.Range.All, first, out range, Period),
            "Min" => Functions.Min<double>(close, System.Range.All, first, out range, Period),
            "MinMax" => Functions.MinMax<double>(close, System.Range.All, first, second, out range, Period),
            "TRange" => Functions.TRange<double>(high, low, close, System.Range.All, first, out range),
            _ => throw new InvalidOperationException("Unknown generated-state operation.")
        };
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Lookback || range.End.Value - range.Start.Value != first.Length)
            throw new InvalidOperationException("Unexpected native generated-state output range.");
    }

    private IIndicator[] Create() => Operation switch
    {
        "Max" => [new HighestHigh(Period)],
        "Min" => [new LowestLow(Period)],
        "MinMax" => [new LowestLow(Period), new HighestHigh(Period)],
        "TRange" => [new TrueRange(1)],
        _ => throw new InvalidOperationException("Unknown generated-state operation.")
    };
    private IIndicatorRun Build(IIndicator[] indicators, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicators)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
    private static double[][] Columns(object values) => values is double[] single ? new[] { single } : (double[][])values;
    private static void Check(double[][] expected, double[][] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Generated-state output count mismatch.");
        for (int i = 0; i < expected.Length; i++) AsinFeasibilityBenchmarks.RequireSame(expected[i], actual[i]);
    }
}
