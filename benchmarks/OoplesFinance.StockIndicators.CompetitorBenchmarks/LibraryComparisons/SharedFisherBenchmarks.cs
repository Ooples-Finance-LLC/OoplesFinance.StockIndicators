using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedFisherBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetFisherTransform")] public string Operation { get; set; } = "Skender.GetFisherTransform";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private (DateTime, double)[] _input = null!;
    private Bar[] _bars = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation != "Skender.GetFisherTransform") throw new ArgumentException("Unknown Fisher operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _input = prices.Select((p, i) => (DateTime.UnixEpoch.AddMinutes(i), p)).ToArray();
        _bars = prices.Select((p, i) => new Bar(_input[i].Item1, p, p + 1, p - 1, p, 1)).ToArray();
        var expected = WindowFisherComparison.Reference(prices, Period, false);
        var nativeExpected = WindowFisherComparison.Reference(prices, Period, true);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        if (serial.Count != Count || parallel.Length != Count) throw new InvalidOperationException("Fisher result length differs.");
        for (int i = 0; i < Count; i++)
        {
            if (serial[i].Date != _input[i].Item1 || parallel[i].Date != _input[i].Item1)
                throw new InvalidOperationException("Fisher result dates differ.");
            RequireSame(nativeExpected[0][i], serial[i].Fisher);
            RequireSame(nativeExpected[1][i], serial[i].Trigger);
            RequireSame(serial[i].Fisher, parallel[i].Fisher);
            RequireSame(serial[i].Trigger, parallel[i].Trigger);
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 2; slot++)
            {
                var values = run[indicator.Outputs[slot]].ToArray();
                var present = run[indicator.Outputs[slot + 2]].ToArray();
                if (values.Length != Count || present.Length != Count) throw new InvalidOperationException("Builder Fisher length differs.");
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), values);
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), present);
            }
        }
    }
    private static void RequireSame(double? expected, double? actual)
    {
        if (expected.HasValue != actual.HasValue || expected.HasValue &&
            BitConverter.DoubleToInt64Bits(expected.Value) != BitConverter.DoubleToInt64Bits(actual!.Value))
            throw new InvalidOperationException("Fisher output or presence differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    // Published tuple overload sorts and calculates eagerly; retain its native list.
    [Benchmark(Baseline = true)] public List<FisherTransformResult> NativeSingle() =>
        (List<FisherTransformResult>)_input.GetFisherTransform(Period);
    [Benchmark] public FisherTransformResult[] NativeParallel()
    {
        var result = new FisherTransformResult[Count];
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            // Fisher's recursive smoothing requires the entire prefix, not P-1 seeds.
            // Charge prefix input preparation and discarded results inside this arm.
            var prefix = _input.Take(end).GetFisherTransform(Period) as IReadOnlyList<FisherTransformResult>
                ?? throw new InvalidOperationException("Native Fisher no longer returns eager results.");
            for (int i = start; i < end; i++) result[i] = prefix[i];
        });
        return result;
    }
    private static WindowFisherTransform Create() => new(Period, useMidpoint: false);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
