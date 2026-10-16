using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedAlmaWindowBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Alma", "Skender.GetAlma")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _inputs = null!;
    private (DateTime, double)[] _prices = null!;
    private (DateTime, double)[][] _segments = null!;
    private int _priorDop;
    private int _chunks;
    private bool FullWindow => Operation == "Skender.GetAlma";

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _chunks = Math.Min(8, Environment.ProcessorCount);
        var prices = Enumerable.Range(0, Count).Select(i => (i * 13 % 101 - 50) / 8d).ToArray();
        var data = CompetitorData.FromCloses(prices);
        _bars = data.IndicatorBars;
        _inputs = prices.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        // Use Skender's public tuple overload: decimal conversion is input
        // preparation, not part of the native calculation being compared.
        _prices = data.Quotes.Select(q => (q.Date, (double)q.Close)).ToArray();
        _segments = Enumerable.Range(0, _chunks).Select(chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            int seed = Math.Max(0, start - Period + 1);
            return _prices[seed..end];
        }).ToArray();
        var pair = AlmaComparison.Create(FullWindow);
        var native = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(pair.CompetitorReference!(data, Period).Outputs["Value"].Values, native);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        var expected = pair.Reference!(data, Period).Outputs["Value"];
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool reference in new[] { false, true })
        {
            var indicator = Create();
            using var run = Build(indicator, history, reference);
            AsinFeasibilityBenchmarks.RequireSame(expected.Values.Select(v => double.IsNaN(v) ? 0 : v).ToArray(), run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Present!.Select(v => v ? 1d : 0d).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun BuilderReference() => Build(Create(), IndicatorHistoryMode.Full, true);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = new double[Count];
        Native(0, Count, _prices, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = new double[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
            Native(Count * chunk / _chunks, Count * (chunk + 1) / _chunks, _segments[chunk], output));
        return output;
    }
    private void Native(int start, int end, (DateTime, double)[] prices, double[] output)
    {
        int seed = Math.Max(0, start - Period + 1);
        if (FullWindow)
        {
            int i = seed;
            foreach (var result in prices.GetAlma(Period, .85, 6))
            {
                if (i >= start) output[i] = result.Alma ?? double.NaN;
                i++;
            }
            return;
        }
        var state = new QuanTAlib.Alma(Period, .85, 6);
        for (int i = seed; i < start; i++) state.Calc(_inputs[i]);
        for (int i = start; i < end; i++) output[i] = state.Calc(_inputs[i]).Value;
    }
    private ArnaudLegouxWindow Create() => new(Period, fullWindowOnly: FullWindow);
    private IIndicatorRun Build(ArnaudLegouxWindow indicator, IndicatorHistoryMode history, bool reference = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
        if (reference) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}
