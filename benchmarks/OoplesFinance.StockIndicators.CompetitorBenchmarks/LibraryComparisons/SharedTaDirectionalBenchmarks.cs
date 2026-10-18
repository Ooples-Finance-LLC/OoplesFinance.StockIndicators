using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaDirectionalBenchmarks
{
    private const int Period = 20;
    [Params("TaLib.Functions.PlusDM", "TaLib.Functions.MinusDM", "TaLib.Functions.PlusDI", "TaLib.Functions.MinusDI",
        "TaLib.Functions.Dx", "TaLib.Functions.Adx", "TaLib.Functions.Adxr")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private CompetitorData _data = null!;
    private PriorDirectionalMeasure _measure;
    private TaDirectionalComparison.Settings? _settings;
    private int _first, _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _measure = (PriorDirectionalMeasure)Array.IndexOf(TaDirectionalComparison.Names, Operation.Split('.')[^1]);
        _settings = new TaDirectionalComparison.Settings(_measure, 0);
        _first = TaDirectionalComparison.NativeLookback(Period, _measure);
        _data = CompetitorData.Create(Count);
        var expected = TaDirectionalComparison.Reference(_data.IndicatorBars, Period, _measure);
        var native = TaDirectionalComparison.NativeReference(_data.IndicatorBars, Period, _measure);
        if (native.Take(_first).Any(v => v.HasValue) || native.Skip(_first).Any(v => !v.HasValue))
            throw new InvalidOperationException("Unexpected directional reference lookback.");
        var nativeValues = native.Skip(_first).Select(v => v!.Value).ToArray();
        AsinFeasibilityBenchmarks.RequireSame(nativeValues, NativeSingle());
        AsinFeasibilityBenchmarks.RequireSame(nativeValues, NativeParallel());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new PriorSeededDirectionalMeasure(_measure, Period);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v ?? 0).ToArray(), run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v.HasValue ? 1d : 0d).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _settings?.Dispose();
        CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new PriorSeededDirectionalMeasure(_measure, Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new PriorSeededDirectionalMeasure(_measure, Period), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_data.IndicatorBars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    [Benchmark(Baseline = true)] public double[] NativeSingle() => Prefix(Count - _first);

    private double[] Prefix(int defined)
    {
        var output = new double[defined];
        // TA-Lib's input endpoint is inclusive; its returned packed range is not.
        var code = TaDirectionalComparison.Call(_data.Highs, _data.Lows, _data.Closes,
            0..(defined + _first - 1), output, out var range, Period, _measure);
        // Native DX reports packed offsets rather than calculation-bar offsets.
        int start = _measure == PriorDirectionalMeasure.Index ? 0 : _first;
        if (code != TALib.Core.RetCode.Success || !range.Equals(new System.Range(start, start + defined)))
            throw new InvalidOperationException("Unexpected directional native status or packed range.");
        return output;
    }

    [Benchmark] public double[] NativeParallel()
    {
        int defined = Count - _first;
        var output = new double[defined];
        int chunks = Math.Min(defined, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(chunks, chunks, worker =>
        {
            int start = defined * worker / chunks, end = defined * (worker + 1) / chunks;
            // Recursive smoothing needs the full prefix, including its original seed.
            // Returning native packed arrays avoids allocating normalized result rows.
            var prefix = Prefix(end);
            prefix.AsSpan(start, end - start).CopyTo(output.AsSpan(start));
        });
        return output;
    }
}
