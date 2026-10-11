using System.Buffers;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRecursiveStateBenchmarks
{
    private const int Period = 20;
    [Params("Ema", "Ad")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _close = null!, _high = null!, _low = null!, _volume = null!;
    private int _previousDop;
    private int Lookback => Operation == "Ema" ? Period - 1 : 0;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _close = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _high = _close.Select(v => v + 1.5).ToArray();
        _low = _close.Select(v => v - 2.5).ToArray();
        _volume = Enumerable.Range(0, Count).Select(i => (double)(1 + i % 7)).ToArray();
        _bars = Enumerable.Range(0, Count).Select(i => new Bar(default, _close[i], _high[i], _low[i], _close[i], _volume[i])).ToArray();
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        var indicator = Create();
        using var ordinary = Build(indicator, IndicatorHistoryMode.Full, true);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var result = Build(indicator, history);
            foreach (var output in indicator.Outputs)
                AsinFeasibilityBenchmarks.RequireSame(ordinary[output].ToArray(), result[output].ToArray());
        }
        // AD's quarter-volume fixture has exact native/library stage agreement.
        // EMA preserves distinct published rounding contracts; route parity above
        // covers every builder bit, and prefix replay must match native serial bits.
        if (Operation == "Ad") AsinFeasibilityBenchmarks.RequireSame(native, ordinary[indicator.Outputs[0]].ToArray());
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Lookback);
        Native(Count, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Lookback);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        // Recursive state cannot be reconstructed from a finite overlap. Each
        // worker replays its complete prefix; serial is also measured as a baseline.
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
            var scratch = ArrayPool<double>.Shared.Rent(end);
            try
            {
                Native(end + Lookback, scratch.AsSpan(0, end));
                scratch.AsSpan(start, end - start).CopyTo(output.AsSpan(start, end - start));
            }
            finally { ArrayPool<double>.Shared.Return(scratch); }
        });
        return output;
    }
    private void Native(int count, Span<double> output)
    {
        System.Range range;
        var status = Operation == "Ema"
            ? Functions.Ema<double>(_close.AsSpan(0, count), System.Range.All, output, out range, Period)
            : Functions.Ad<double>(_high.AsSpan(0, count), _low.AsSpan(0, count), _close.AsSpan(0, count), _volume.AsSpan(0, count), System.Range.All, output, out range);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Lookback || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected recursive native output range.");
    }
    private IIndicator Create() => Operation == "Ema" ? new Ema(Period) : new Adl(Period);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
