using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedForceIndexBenchmarks
{
    private const int Period = 13;
    [Params("Skender.GetForceIndex")] public string Operation { get; set; } = "Skender.GetForceIndex";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Quote[] _quotes = null!;
    private Bar[] _bars = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "Skender.GetForceIndex") throw new ArgumentException("Unsupported Force Index operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var closes = Enumerable.Range(0, Count).Select(i => 10 + (i / 2 * 13 % 101) / 8d).ToArray();
            var volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 17 + 1)).ToArray();
            var data = CompetitorData.FromOhlcv(closes, closes.Select(v => v + 1).ToArray(),
                closes.Select(v => v - 1).ToArray(), closes, volumes);
            _quotes = data.Quotes.ToArray(); _bars = data.IndicatorBars;
            _chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
            var pair = ComparisonPairs.Get(Operation);
            var expected = pair.Reference!(data, Period).Outputs["Value"];
            var nativeExpected = pair.CompetitorReference!(data, Period).Outputs["Value"];
            CheckNative(NativeSingle(), nativeExpected);
            CheckNative(NativeParallel(), nativeExpected);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new SeededForceIndex(Period);
                using var run = Build(indicator, history);
                if (run[indicator.Value].Length != Count || run[indicator.IsDefined].Length != Count)
                    throw new InvalidOperationException("Force Index column length differs.");
                for (int i = 0; i < Count; i++)
                {
                    bool present = expected.Present![i];
                    double value = present ? expected.Values[i] : 0;
                    if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(run[indicator.Value][i])
                        || run[indicator.IsDefined][i] != (present ? 1d : 0d))
                        throw new InvalidOperationException($"Exact Force Index output differs at {i}.");
                    // Native uses rounded alpha/difference/addition; both paths
                    // have separately passed their bitwise arithmetic references.
                    if (present != nativeExpected.Present![i] || present
                        && Math.Abs(value - nativeExpected.Values[i]) > 1e-10)
                        throw new InvalidOperationException($"Native and exact Force Index disagree at {i}.");
                }
            }
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (!_settingsSaved) return;
        CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
        _settingsSaved = false;
    }

    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle() => _quotes.GetForceIndex(Period);
    [Benchmark]
    public object NativeParallel()
    {
        var result = new ForceIndexResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            // Recursive state needs the complete prefix. Native replay, quote
            // conversion and owned-row publication all remain inside timing.
            int index = 0;
            foreach (var row in _quotes.Take(end).GetForceIndex(Period))
            {
                if (index >= start) result[index] = row;
                index++;
            }
        });
        return result;
    }

    private IIndicatorRun Build(SeededForceIndex indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    private void CheckNative(object result, ComparisonOutput expected)
    {
        if (result is not IReadOnlyList<ForceIndexResult> rows || rows.Count != Count)
            throw new InvalidOperationException("Native Force Index must retain its complete eager result.");
        for (int i = 0; i < Count; i++)
        {
            bool present = expected.Present![i];
            if (rows[i].Date != _quotes[i].Date || rows[i].ForceIndex.HasValue != present
                || present && BitConverter.DoubleToInt64Bits(rows[i].ForceIndex!.Value)
                    != BitConverter.DoubleToInt64Bits(expected.Values[i]))
                throw new InvalidOperationException($"Native Force Index alignment or value differs at {i}.");
        }
    }
}
