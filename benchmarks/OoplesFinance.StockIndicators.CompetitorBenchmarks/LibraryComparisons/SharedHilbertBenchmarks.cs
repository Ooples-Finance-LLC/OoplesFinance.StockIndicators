using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedHilbertBenchmarks
{
    private const int Lookback = 32;
    [Params("TaLib.Functions.HtDcPeriod", "TaLib.Functions.HtPhasor", "TaLib.Functions.Mama")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private double[] _prices = null!;
    private Bar[] _bars = null!;
    private int _outputs, _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var unstable = Operation switch
        {
            "TaLib.Functions.HtDcPeriod" => TaCore.UnstableFunc.HtDcPeriod,
            "TaLib.Functions.HtPhasor" => TaCore.UnstableFunc.HtPhasor,
            "TaLib.Functions.Mama" => TaCore.UnstableFunc.Mama,
            _ => throw new ArgumentException("Unknown Hilbert operation.")
        };
        if (Count <= Lookback) throw new ArgumentOutOfRangeException(nameof(Count));
        if (TaCore.CompatibilitySettings.Get() != TaCore.CompatibilityMode.Default
            || TaCore.UnstablePeriodSettings.Get(unstable) != 0)
            throw new InvalidOperationException("Hilbert benchmarks require default native settings.");
        _outputs = Operation == "TaLib.Functions.HtDcPeriod" ? 1 : 2;
        _prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _bars = _prices.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();

        // Separate independent models retain each library's stage-rounding
        // contract. None of their normalization or validation is measured.
        bool mama = Operation == "TaLib.Functions.Mama";
        var expected = mama ? SeededPhaseComparison.GridReference(_prices, .5, .05, false, true)
            : HilbertCycleComparison.Reference(_prices, _outputs == 1, 0);
        var nativeExpected = mama ? DelayedPhaseComparison.NativePacked(_prices, .5, .05, 0, 0, Count - 1)
            : HilbertCycleComparison.NativePacked(_prices, _outputs == 1, 0, 0, Count - 1);
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        int mature = Count - Lookback;
        for (int slot = 0; slot < _outputs; slot++)
        {
            AsinFeasibilityBenchmarks.RequireSame(nativeExpected[slot], native.AsSpan(slot * mature, mature).ToArray());
            for (int i = Lookback; i < Count; i++)
                if (!expected[slot][i].HasValue || !double.IsFinite(native[slot * mature + i - Lookback])
                    || Math.Abs(native[slot * mature + i - Lookback] - expected[slot][i]!.Value) > 1e-9)
                    throw new InvalidOperationException("Native Hilbert output differs from the owned reference on benchmark inputs.");
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            for (int slot = 0; slot < _outputs; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(),
                    run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray(),
                    run[indicator.Outputs[slot + _outputs]].ToArray());
            }
        }
    }

    private MultiOutputIndicatorBase Create() => Operation switch
    {
        "TaLib.Functions.HtDcPeriod" => new HilbertCyclePeriod(),
        "TaLib.Functions.HtPhasor" => new HilbertPhasor(),
        "TaLib.Functions.Mama" => new DelayedPhaseAdaptiveAverage(),
        _ => throw new InvalidOperationException("Unknown Hilbert operation.")
    };
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);

    [Benchmark(Baseline = true)]
    public double[] NativeSingle()
    {
        // Disjoint output spans share one retained packed allocation; native
        // mature values are not expanded into full-length presence/value arrays.
        var result = new double[(Count - Lookback) * _outputs];
        Native(_prices, result);
        return result;
    }

    [Benchmark]
    public double[] NativeParallel()
    {
        int mature = Count - Lookback;
        var result = new double[mature * _outputs];
        int chunks = Math.Min(mature, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = mature * chunk / chunks, end = mature * (chunk + 1) / chunks;
            // Replay the complete prefix: Hilbert phase/parity and adaptive
            // recurrence state cannot be independently reseeded at a chunk.
            var prefix = new double[end * _outputs];
            Native(_prices.AsSpan(0, end + Lookback), prefix);
            for (int slot = 0; slot < _outputs; slot++)
                prefix.AsSpan(slot * end + start, end - start).CopyTo(result.AsSpan(slot * mature + start, end - start));
        });
        return result;
    }

    private void Native(ReadOnlySpan<double> input, Span<double> output)
    {
        int mature = input.Length - Lookback;
        var first = output[..mature];
        var second = _outputs == 1 ? Span<double>.Empty : output.Slice(mature, mature);
        System.Range range;
        var code = Operation switch
        {
            "TaLib.Functions.HtDcPeriod" => Functions.HtDcPeriod<double>(input, System.Range.All, first, out range),
            "TaLib.Functions.HtPhasor" => Functions.HtPhasor<double>(input, System.Range.All, first, second, out range),
            "TaLib.Functions.Mama" => Functions.Mama<double>(input, System.Range.All, first, second, out range, .5, .05),
            _ => throw new InvalidOperationException("Unknown Hilbert operation.")
        };
        if (code != TaCore.RetCode.Success || range.Start.Value != Lookback || range.End.Value != input.Length)
            throw new InvalidOperationException("Unexpected native Hilbert output range.");
    }

    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
