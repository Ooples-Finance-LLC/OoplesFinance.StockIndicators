using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedMoneyFlowIndexBenchmarks
{
    private const int Period = 14;
    [Params("TaLib.Functions.Mfi")] public string Operation { get; set; } = "TaLib.Functions.Mfi";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _highs = null!, _lows = null!, _closes = null!, _volumes = null!;
    private int _priorDop;
    private int _priorUnstable;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "TaLib.Functions.Mfi") throw new ArgumentException("Unsupported money-flow operation.");
        if (Count <= Period) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _priorUnstable = TALib.Core.UnstablePeriodSettings.Get(TALib.Core.UnstableFunc.Mfi);
        _settingsSaved = true;
        try
        {
            TALib.Core.UnstablePeriodSettings.Set(TALib.Core.UnstableFunc.Mfi, 0);
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            _closes = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
            _highs = _closes.Select((v, i) => v + 1 + (i % 5) / 16d).ToArray();
            _lows = _closes.Select((v, i) => v - 1 - (i % 7) / 16d).ToArray();
            _volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 13 + 1)).ToArray();
            _bars = _closes.Select((v, i) => new Bar(default, v, _highs[i], _lows[i], v, _volumes[i])).ToArray();
            var native = NativeSingle();
            CheckNative(native, NativeParallel());
            var expected = MoneyFlowIndexComparison.Reference(_bars, Period, true);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new WindowMoneyFlowIndex(Period, minimumTotalOne: true);
                using var run = Build(indicator, history);
                for (int i = 0; i < Count; i++)
                {
                    if (BitConverter.DoubleToInt64Bits(expected[i] ?? 0) != BitConverter.DoubleToInt64Bits(run[indicator.Value][i])
                        || run[indicator.IsDefined][i] != (expected[i].HasValue ? 1d : 0d))
                        throw new InvalidOperationException($"Exact MFI builder mismatch at {i}.");
                }
                CheckNative(native, run[indicator.Value].Slice(Period).ToArray());
            }
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (!_settingsSaved) return;
        TALib.Core.UnstablePeriodSettings.Set(TALib.Core.UnstableFunc.Mfi, _priorUnstable);
        CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
        _settingsSaved = false;
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowMoneyFlowIndex(Period, minimumTotalOne: true), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowMoneyFlowIndex(Period, minimumTotalOne: true), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    [Benchmark(Baseline = true)]
    public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period);
        Native(0, Count, output);
        return output;
    }

    [Benchmark]
    public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Period);
        int chunks = Math.Min(output.Length, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
            Native(start, end - start + Period, output.AsSpan(start, end - start));
        });
        return output;
    }

    private void Native(int start, int count, Span<double> output)
    {
        var status = TALib.Functions.Mfi<double>(_highs.AsSpan(start, count), _lows.AsSpan(start, count),
            _closes.AsSpan(start, count), _volumes.AsSpan(start, count), System.Range.All, output, out var range, Period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native MFI output range.");
    }

    private static void CheckNative(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("MFI output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(expected[i]) || !double.IsFinite(actual[i])
                || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Native MFI mismatch at {i}.");
    }
}

