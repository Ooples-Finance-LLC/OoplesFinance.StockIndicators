using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedCmfBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetCmf")] public string Operation { get; set; } = "Skender.GetCmf";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Quote[] _quotes = null!;
    private Bar[] _bars = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "Skender.GetCmf") throw new ArgumentException("Unsupported CMF operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var lows = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
            var highs = lows.Select((v, i) => v + (i % 31 == 0 ? 0 : 1 << (i % 3))).ToArray();
            var closes = lows.Select((v, i) => v + (highs[i] - v) * (i % 9) / 8d).ToArray();
            var volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 13 + 1)).ToArray();
            var data = CompetitorData.FromOhlcv(lows, highs, lows, closes, volumes);
            _quotes = data.Quotes.ToArray(); _bars = data.IndicatorBars;
            _chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
            var pair = MoneyFlowDetailComparison.Pair;
            var expected = pair.Reference!(data, Period);
            var nativeExpected = pair.CompetitorReference!(data, Period);
            CheckNative(NativeSingle(), nativeExpected);
            CheckNative(NativeParallel(), nativeExpected);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new MoneyFlowWithDetails(Period);
                using var run = Build(indicator, history);
                foreach (var output in indicator.Outputs)
                    if (run[output].Length != Count) throw new InvalidOperationException("CMF column length differs.");
                for (int slot = 0; slot < 3; slot++)
                {
                    var reference = expected.Outputs[MoneyFlowDetailComparison.Names[slot]];
                    var native = nativeExpected.Outputs[MoneyFlowDetailComparison.Names[slot]];
                    for (int i = 0; i < Count; i++)
                    {
                        bool present = reference.Present![i];
                        double value = present ? reference.Values[i] : 0;
                        if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(run[indicator.Outputs[slot]][i])
                            || slot == 2 && run[indicator.CmfIsDefined][i] != (present ? 1d : 0d))
                            throw new InvalidOperationException($"Exact CMF column {slot} differs at {i}.");
                        if (present != native.Present![i] || present && Math.Abs(value - native.Values[i]) > 1e-12)
                            throw new InvalidOperationException($"Native and exact CMF column {slot} disagree at {i}.");
                    }
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
    [Benchmark(Baseline = true)] public object NativeSingle() => _quotes.GetCmf(Period);
    [Benchmark]
    public object NativeParallel()
    {
        var result = new CmfResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            int first = Math.Max(0, start - Period + 1), index = first;
            // Native CMF re-sums each fixed window. A period-1 halo supplies
            // the complete window without changing its addition order.
            foreach (var row in _quotes.Skip(first).Take(end - first).GetCmf(Period))
            {
                if (index >= start) result[index] = row;
                index++;
            }
        });
        return result;
    }

    private IIndicatorRun Build(MoneyFlowWithDetails indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    private void CheckNative(object result, ComparisonSeries expected)
    {
        if (result is not IReadOnlyList<CmfResult> rows || rows.Count != Count)
            throw new InvalidOperationException("Native CMF must retain its complete eager result.");
        for (int i = 0; i < Count; i++)
        {
            if (rows[i].Date != _quotes[i].Date) throw new InvalidOperationException("Native CMF date differs.");
            double?[] values = [rows[i].MoneyFlowMultiplier, rows[i].MoneyFlowVolume, rows[i].Cmf];
            for (int slot = 0; slot < 3; slot++)
            {
                var reference = expected.Outputs[MoneyFlowDetailComparison.Names[slot]];
                if (values[slot].HasValue != reference.Present![i] || values[slot].HasValue
                    && BitConverter.DoubleToInt64Bits(values[slot]!.Value) != BitConverter.DoubleToInt64Bits(reference.Values[i]))
                    throw new InvalidOperationException($"Native CMF column {slot} differs at {i}.");
            }
        }
    }
}
