using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedEngulfingBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _open = null!, _close = null!, _high = null!, _low = null!;
    private int _previousDop;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var fixture = CandleComparison.Fixture((Count - 1) / 512 + 1);
        _open = fixture.Opens.Take(Count).ToArray();
        _close = fixture.Closes.Take(Count).ToArray();
        _high = fixture.Highs.Take(Count).ToArray();
        _low = fixture.Lows.Take(Count).ToArray();
        _bars = Enumerable.Range(0, Count).Select(i => new Bar(default, _open[i], _high[i], _low[i], _close[i], 1)).ToArray();
        var expected = NativeSingle();
        if (!expected.SequenceEqual(NativeParallel())) throw new InvalidOperationException("Native parallel pattern mismatch.");
        var reference = CandleComparison.Reference(CompetitorData.FromOhlc(_open, _high, _low, _close)).Outputs["Value"];
        AsinFeasibilityBenchmarks.RequireSame(reference.Values.AsSpan(2).ToArray(), expected.Select(v => (double)v).ToArray());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool ordinary in new[] { false, true })
        {
            var indicator = new EngulfingPattern();
            using var run = Build(indicator, history, ordinary);
            AsinFeasibilityBenchmarks.RequireSame(reference.Values, run[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new EngulfingPattern(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new EngulfingPattern(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(new EngulfingPattern(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public int[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<int>(Count - 2);
        Native(_open, _high, _low, _close, output);
        return output;
    }
    [Benchmark] public int[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<int>(Count - 2);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, size = output.Length * (chunk + 1) / chunks - start;
            Native(_open.AsSpan(start, size + 2), _high.AsSpan(start, size + 2),
                _low.AsSpan(start, size + 2), _close.AsSpan(start, size + 2), output.AsSpan(start, size));
        });
        return output;
    }
    private static void Native(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<int> output)
    {
        var status = Candles.Engulfing<double>(open, high, low, close, System.Range.All, output, out var range);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != 2 || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native engulfing output range.");
    }
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool ordinary = false,
        IndicatorExecutionBackend backend = IndicatorExecutionBackend.Cpu)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(backend);
        if (ordinary) builder.ConfigureBehavior(_ => { });
        var result = builder.BuildAsync().GetAwaiter().GetResult();
        if (!ordinary && builder.LastExecution?.Backend != backend)
            throw new InvalidOperationException("Requested engulfing backend did not execute.");
        return result;
    }
}
