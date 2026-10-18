using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedScaledTrueRangeBenchmarks
{
    private const int Divisor = 20;
    [Params("QuanTAlib.Atr")] public string Operation { get; set; } = "QuanTAlib.Atr";
    [Params(1_000, 10_000)] public int Count { get; set; }
    internal bool ValidateGpu { get; init; } = true;
    private CompetitorData _data = null!;
    private int _priorDop, _chunks;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "QuanTAlib.Atr") throw new ArgumentException("Unsupported scaled range operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var closes = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
            var highs = closes.Select((v, i) => v + (i % 3 + 1) / 4d).ToArray();
            var lows = closes.Select(v => v - .5).ToArray();
            _data = CompetitorData.FromOhlcv(closes, highs, lows, closes, Enumerable.Repeat(1d, Count).ToArray());
            _chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
            var pair = QuanAtrComparison.Pair;
            var exact = pair.Reference!(_data, Divisor).Outputs["Value"].Values;
            var native = pair.CompetitorReference!(_data, Divisor).Outputs["Value"].Values;
            Check(native, NativeSingle()); Check(native, NativeParallel());
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            foreach (bool gpu in ValidateGpu ? new[] { false, true } : new[] { false })
            {
                var indicator = new ScaledTrueRange(Divisor);
                using var run = Build(indicator, history, gpu);
                Check(exact, run[indicator.Value].ToArray());
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

    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(Divisor), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(Divisor), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(new(Divisor), IndicatorHistoryMode.Full, true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(new(Divisor), IndicatorHistoryMode.LatestOnly, true);
    private IIndicatorRun Build(ScaledTrueRange indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_data.IndicatorBars))
            .ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose(); throw new InvalidOperationException("Required scaled range GPU did not execute on-device.");
        }
        return run;
    }

    [Benchmark(Baseline = true)]
    public double[] NativeSingle()
    {
        var state = new QuanTAlib.Atr(Divisor);
        var output = new double[Count];
        for (int i = 0; i < Count; i++) output[i] = state.Calc(_data.Bars[i]).Value;
        return output;
    }

    [Benchmark]
    public double[] NativeParallel()
    {
        var output = new double[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            var state = new QuanTAlib.Atr(Divisor);
            // Pinned bar-route ATR uses the previous close but does not retain
            // a temporal mean. One preceding bar reproduces its native result.
            if (start > 0) state.Calc(_data.Bars[start - 1]);
            for (int i = start; i < end; i++) output[i] = state.Calc(_data.Bars[i]).Value;
        });
        return output;
    }

    private static void Check(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Scaled range length differs.");
        AsinFeasibilityBenchmarks.RequireSame(expected, actual);
    }
}
