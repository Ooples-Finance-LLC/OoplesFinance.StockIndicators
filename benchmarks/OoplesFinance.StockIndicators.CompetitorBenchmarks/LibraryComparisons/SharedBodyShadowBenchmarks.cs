using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedBodyShadowBenchmarks
{
    [Params("BeltHold", "Marubozu", "ClosingMarubozu", "SpinningTop", "HighWave", "LongLine", "ShortLine")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private const int Lookback = 10;
    private Bar[] _bars = null!;
    private double[] _open = null!, _high = null!, _low = null!, _close = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var fixture = BodyShadowComparison.Fixture().IndicatorBars;
        _bars = Enumerable.Range(0, Count).Select(i => fixture[i % fixture.Length]).ToArray();
        _open = _bars.Select(b => b.Open).ToArray(); _high = _bars.Select(b => b.High).ToArray();
        _low = _bars.Select(b => b.Low).ToArray(); _close = _bars.Select(b => b.Close).ToArray();
        var data = CompetitorData.FromOhlc(_open, _high, _low, _close);
        var pair = BodyShadowComparison.Pairs.Single(p => p.Id == "TaLib.Candles." + Operation);
        var oracle = pair.Reference!(data, Lookback).Outputs["Value"];
        if (oracle.FirstValid != Lookback) throw new InvalidOperationException("Unexpected body/shadow reference alignment.");
        var native = NativeSingle();
        if (!native.SequenceEqual(NativeParallel()) || !native.Contains(100) || !native.Contains(-100))
            throw new InvalidOperationException("Body/shadow fixture must preserve partitions and exercise both pattern directions.");
        AsinFeasibilityBenchmarks.RequireSame(oracle.Values.AsSpan(Lookback).ToArray(), native.Select(v => (double)v).ToArray());
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            var indicator = Create();
            using var run = Build(indicator, history, reference);
            AsinFeasibilityBenchmarks.RequireSame(oracle.Values, run[indicator].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public int[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<int>(Count - Lookback);
        Native(_open, _high, _low, _close, output);
        return output;
    }
    [Benchmark] public int[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<int>(Count - Lookback);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, count = output.Length * (chunk + 1) / chunks - start;
            Native(_open.AsSpan(start, count + Lookback), _high.AsSpan(start, count + Lookback),
                _low.AsSpan(start, count + Lookback), _close.AsSpan(start, count + Lookback), output.AsSpan(start, count));
        });
        return output;
    }
    private void Native(ReadOnlySpan<double> open, ReadOnlySpan<double> high, ReadOnlySpan<double> low,
        ReadOnlySpan<double> close, Span<int> output)
    {
        System.Range range;
        var status = Operation switch
        {
            "BeltHold" => Candles.BeltHold<double>(open, high, low, close, System.Range.All, output, out range),
            "Marubozu" => Candles.Marubozu<double>(open, high, low, close, System.Range.All, output, out range),
            "ClosingMarubozu" => Candles.ClosingMarubozu<double>(open, high, low, close, System.Range.All, output, out range),
            "SpinningTop" => Candles.SpinningTop<double>(open, high, low, close, System.Range.All, output, out range),
            "HighWave" => Candles.HighWave<double>(open, high, low, close, System.Range.All, output, out range),
            "LongLine" => Candles.LongLine<double>(open, high, low, close, System.Range.All, output, out range),
            "ShortLine" => Candles.ShortLine<double>(open, high, low, close, System.Range.All, output, out range),
            _ => throw new InvalidOperationException("Unknown body/shadow operation.")
        };
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Lookback || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native body/shadow output range.");
    }
    private IIndicator Create() => BodyShadowComparison.Create(Operation);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
