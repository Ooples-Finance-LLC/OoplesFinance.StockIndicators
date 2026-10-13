using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedContainmentCandleBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    [Params("TaLib.Candles.Harami", "TaLib.Candles.HaramiCross", "TaLib.Candles.HomingPigeon", "TaLib.Candles.DojiStar")]
    public string Operation { get; set; } = "";
    private string _name = null!;
    private const int Warmup = 11;
    private Bar[] _bars = null!;
    private double[] _open = null!, _high = null!, _low = null!, _close = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _name = Operation.StartsWith("TaLib.Candles.") ? Operation[14..] : throw new ArgumentException("Unknown operation.");
        _ = ContainmentCandleComparison.Create(_name);
        var fixture = ContainmentCandleComparison.Fixture().IndicatorBars;
        if (Count < 2) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            var c = fixture[i % fixture.Length];
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), c.Open, c.High, c.Low, c.Close, 1);
        }).ToArray();
        _open = _bars.Select(b => b.Open).ToArray(); _high = _bars.Select(b => b.High).ToArray();
        _low = _bars.Select(b => b.Low).ToArray(); _close = _bars.Select(b => b.Close).ToArray();
        var data = KickingRickshawComparison.FromBars(_bars);
        var pair = ComparisonPairs.Get(Operation);
        var expected = pair.Reference!(data, 20).Outputs["Value"].Values;
        var serial = NativeSingle(); var parallel = NativeParallel();
        AsinFeasibilityBenchmarks.RequireSame(expected, serial.Select(v => (double)v).ToArray());
        AsinFeasibilityBenchmarks.RequireSame(expected, parallel.Select(v => (double)v).ToArray());
        if (Count >= 1000 && (!serial.Contains(100) || !serial.Skip(Warmup).Contains(0) || _name != "HomingPigeon" && !serial.Contains(-100)))
            throw new InvalidOperationException("Containment fixture must include matches and nonmatches.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = ContainmentCandleComparison.Create(_name);
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator.Outputs[0]].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(ContainmentCandleComparison.Create(_name), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(ContainmentCandleComparison.Create(_name), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public int[] NativeSingle()
    {
        var result = new int[Count];
        var code = Call(_open, _high, _low, _close, result, out var range);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("Native containment failed.");
        var (offset, count) = range.GetOffsetAndLength(Count);
        if (count != Math.Max(0, Count - Warmup) || count > 0 && offset != Warmup)
            throw new InvalidOperationException("Native containment range changed.");
        if (count > 0) { Array.Copy(result, 0, result, offset, count); Array.Clear(result, 0, offset); }
        return result;
    }
    [Benchmark] public int[] NativeParallel()
    {
        int workers = Math.Min(Count / 2, Math.Min(8, Environment.ProcessorCount));
        var result = new int[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, int[] result)
    {
        int from = Math.Max(0, start - Warmup), length = end - from;
        var packed = new int[length];
        var code = Call(_open.AsSpan(from, length), _high.AsSpan(from, length),
            _low.AsSpan(from, length), _close.AsSpan(from, length), packed, out var range);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("Native containment failed.");
        var (offset, count) = range.GetOffsetAndLength(length);
        if (count != Math.Max(0, length - Warmup) || count > 0 && offset != Warmup)
            throw new InvalidOperationException("Native containment range changed.");
        for (int i = 0; i < count; i++)
        {
            int index = from + offset + i;
            if (index >= start) result[index] = packed[i];
        }
    }
    private TALib.Core.RetCode Call(ReadOnlySpan<double> open, ReadOnlySpan<double> high,
        ReadOnlySpan<double> low, ReadOnlySpan<double> close, Span<int> output, out System.Range range) => _name switch
    {
        "Harami" => Candles.Harami<double>(open, high, low, close, System.Range.All, output, out range),
        "HaramiCross" => Candles.HaramiCross<double>(open, high, low, close, System.Range.All, output, out range),
        "HomingPigeon" => Candles.HomingPigeon<double>(open, high, low, close, System.Range.All, output, out range),
        "DojiStar" => Candles.DojiStar<double>(open, high, low, close, System.Range.All, output, out range),
        _ => throw new InvalidOperationException("Unknown containment operation.")
    };
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
