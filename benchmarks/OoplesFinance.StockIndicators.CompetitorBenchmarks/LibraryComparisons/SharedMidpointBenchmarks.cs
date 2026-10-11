using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedMidpointBenchmarks
{
    private const int Period = 20;
    [Params("MidPoint", "MidPrice")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!, _highs = null!, _lows = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _closes = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        _highs = _closes.Select((v, i) => v + (i % 7 + 1) / 8d).ToArray();
        _lows = _closes.Select((v, i) => v - (i % 5 + 1) / 8d).ToArray();
        _bars = _closes.Select((v, i) => new Bar(default, v, _highs[i], _lows[i], v, 1)).ToArray();
        var expected = Enumerable.Range(0, Count).Select(i =>
        {
            int start = Math.Max(0, i - Period + 1), length = i - start + 1;
            double low = (Operation == "MidPrice" ? _lows : _closes).Skip(start).Take(length).Min();
            double high = (Operation == "MidPrice" ? _highs : _closes).Skip(start).Take(length).Max();
            // Decimal is exact for the bounded eighth-integer fixture.
            decimal midpoint = ((decimal)low + (decimal)high) / 2m;
            return midpoint == 0m ? 0d : (double)midpoint;
        }).ToArray();
        // This binary-grid fixture has exact midpoints for both contracts.
        // Native returns only its defined suffix; the builder also owns startup.
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(expected.Skip(Period - 1).ToArray(), native);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            var indicator = Create();
            using var run = Build(indicator, history, reference);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = new double[Count - Period + 1];
        Native(Period - 1, Count, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = new double[Count - Period + 1];
        int chunks = Math.Min(8, Environment.ProcessorCount), length = output.Length;
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            Native(Period - 1 + length * chunk / chunks, Period - 1 + length * (chunk + 1) / chunks, output));
        return output;
    }
    private void Native(int start, int end, double[] output)
    {
        int seed = start - Period + 1;
        var destination = output.AsSpan(seed, end - start);
        System.Range range;
        var code = Operation == "MidPoint"
            ? Functions.MidPoint<double>(_closes.AsSpan(seed, end - seed), System.Range.All, destination, out range, Period)
            : Functions.MidPrice<double>(_highs.AsSpan(seed, end - seed), _lows.AsSpan(seed, end - seed), System.Range.All, destination, out range, Period);
        if (code != TALib.Core.RetCode.Success || range.Start.Value != Period - 1 || range.End.Value != end - seed)
            throw new InvalidOperationException("Unexpected native midpoint output range.");
    }
    private WindowRangeMidpoint Create() => new(Period, Operation == "MidPrice");
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
