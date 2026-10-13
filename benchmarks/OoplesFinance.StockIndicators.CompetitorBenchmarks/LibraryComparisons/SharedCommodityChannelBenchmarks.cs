using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedCommodityChannelBenchmarks
{
    private const int Period = 20;
    [Params("TaLib.Functions.Cci")] public string Operation { get; set; } = "TaLib.Functions.Cci";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _highs = null!, _lows = null!, _closes = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "TaLib.Functions.Cci") throw new ArgumentException("Unsupported commodity-channel operation.");
        if (Count < Period) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _closes = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _highs = _closes.Select((v, i) => v + 1 + (i % 5) / 16d).ToArray();
        _lows = _closes.Select((v, i) => v - 1 - (i % 7) / 16d).ToArray();
        _bars = _closes.Select((v, i) => new Bar(default, v, _highs[i], _lows[i], v, 1)).ToArray();
        var native = NativeSingle();
        var parallel = NativeParallel();
        var inputs = _bars.Select(b => (b.High, b.Low, b.Close)).ToArray();
        var nativeExpected = CommodityChannelComparison.FloatingReference(inputs, Period, true);
        AsinFeasibilityBenchmarks.RequireSame(nativeExpected.Skip(Period - 1).Select(v => v!.Value).ToArray(), native);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int start = native.Length * chunk / chunks, end = native.Length * (chunk + 1) / chunks;
            // Each native partition starts a fresh circular buffer. Check its
            // own summation order exactly, then bound serial/parallel drift.
            var reference = CommodityChannelComparison.FloatingReference(
                inputs.Skip(start).Take(end - start + Period - 1).ToArray(), Period, true);
            AsinFeasibilityBenchmarks.RequireSame(reference.Skip(Period - 1).Select(v => v!.Value).ToArray(),
                parallel.AsSpan(start, end - start).ToArray());
        }
        CheckNative(native, parallel);
        var expected = CommodityChannelComparison.Reference(_bars, Period, 1);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new WindowCommodityChannelIndex(Period, flatZero: true);
            using var run = Build(indicator, history);
            for (int i = 0; i < Count; i++)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i] ?? 0) != BitConverter.DoubleToInt64Bits(run[indicator.Value][i])
                    || run[indicator.IsDefined][i] != (expected[i].HasValue ? 1d : 0d))
                    throw new InvalidOperationException($"Exact CCI builder mismatch at {i}.");
            }
            CheckNative(native, run[indicator.Value].Slice(Period - 1).ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowCommodityChannelIndex(Period, flatZero: true), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowCommodityChannelIndex(Period, flatZero: true), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    [Benchmark(Baseline = true)]
    public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        Native(0, Count, output);
        return output;
    }

    [Benchmark]
    public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period + 1);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
            Native(start, end - start + Period - 1, output.AsSpan(start, end - start));
        });
        return output;
    }

    private void Native(int start, int count, Span<double> output)
    {
        var status = TALib.Functions.Cci<double>(_highs.AsSpan(start, count), _lows.AsSpan(start, count),
            _closes.AsSpan(start, count), System.Range.All, output, out var range, Period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period - 1
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native CCI output range.");
    }

    private static void CheckNative(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("CCI output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(expected[i]) || !double.IsFinite(actual[i])
                || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Native CCI mismatch at {i}.");
    }
}
