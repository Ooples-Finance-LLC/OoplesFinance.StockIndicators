using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPointwiseBatchBenchmarks
{
    [Params("Trigonometry", "Arithmetic")] public string Group { get; set; } = "";
    [Params(10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _close = null!, _open = null!;
    private int _previousDop;
    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _close = Enumerable.Range(0, Count).Select(i => .25 + (i % 37) / 128d).ToArray();
        _open = Enumerable.Range(0, Count).Select(i => .5 + (i % 23) / 128d).ToArray();
        _bars = Enumerable.Range(0, Count).Select(i => new Bar(default, _open[i], 2, 0, _close[i], 1)).ToArray();
        var native = NativeSingle();
        var parallel = NativeParallel();
        var indicators = Create();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = Build(indicators, history);
            using var reference = Build(indicators, history, true);
            for (int slot = 0; slot < indicators.Length; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(native[slot], parallel[slot]);
                AsinFeasibilityBenchmarks.RequireSame(native[slot], run[indicators[slot].Outputs[0]].ToArray());
                foreach (var output in indicators[slot].Outputs)
                    AsinFeasibilityBenchmarks.RequireSame(reference[output].ToArray(), run[output].ToArray());
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark] public IIndicatorRun[] SeparateFull() => Create().Select(i => Build([i], IndicatorHistoryMode.Full)).ToArray();
    [Benchmark(Baseline = true)] public double[][] NativeSingle()
    {
        var output = Allocate();
        Native(0, Count, output);
        return output;
    }
    [Benchmark] public double[][] NativeParallel()
    {
        var output = Allocate();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            Native(start, end - start, output);
        });
        return output;
    }
    private double[][] Allocate() => [new double[Count], new double[Count], new double[Count]];
    private void Native(int start, int count, double[][] output)
    {
        var close = _close.AsSpan(start, count); var open = _open.AsSpan(start, count);
        for (int slot = 0; slot < 3; slot++)
        {
            System.Range range;
            var values = output[slot].AsSpan(start, count);
            var status = (Group, slot) switch
            {
                ("Trigonometry", 0) => Functions.Sin<double>(close, System.Range.All, values, out range),
                ("Trigonometry", 1) => Functions.Cos<double>(close, System.Range.All, values, out range),
                ("Trigonometry", _) => Functions.Atan<double>(close, System.Range.All, values, out range),
                ("Arithmetic", 0) => Functions.Add<double>(close, open, System.Range.All, values, out range),
                ("Arithmetic", 1) => Functions.Sub<double>(close, open, System.Range.All, values, out range),
                ("Arithmetic", _) => Functions.Mult<double>(close, open, System.Range.All, values, out range),
                _ => throw new InvalidOperationException("Unknown pointwise group.")
            };
            if (status != TALib.Core.RetCode.Success || range.GetOffsetAndLength(count) != (0, count))
                throw new InvalidOperationException("Native batch range mismatch.");
        }
    }
    private IIndicator[] Create() => Group switch
    {
        "Trigonometry" => [new PriceCircularTransform(PriceCircularOperation.Sine),
            new PriceCircularTransform(PriceCircularOperation.Cosine), new PriceCircularTransform(PriceCircularOperation.ArcTangent)],
        "Arithmetic" => [new CandleArithmetic(CandleArithmeticOperation.Add),
            new CandleArithmetic(CandleArithmeticOperation.Subtract), new CandleArithmetic(CandleArithmeticOperation.Multiply)],
        _ => throw new InvalidOperationException("Unknown pointwise group.")
    };
    private IIndicatorRun Build(IIndicator[] indicators, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
