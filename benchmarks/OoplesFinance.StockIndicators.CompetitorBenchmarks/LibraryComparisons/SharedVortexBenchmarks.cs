using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedVortexBenchmarks
{
    private const int Period = 14;
    [Params("Skender.GetVortex")] public string Operation { get; set; } = "Skender.GetVortex";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private Quote[] _quotes = null!;
    private Quote[][] _segments = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "Skender.GetVortex") throw new ArgumentException("Unsupported Vortex operation.");
        if (Count <= Period) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            _bars = Enumerable.Range(0, Count).Select(i =>
            {
                double close = 10 + (i * 13 % 101) / 8d;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), close,
                    close + 1 + (i % 5) / 16d, close - 1 - (i % 7) / 16d, close, 1);
            }).ToArray();
            // Identical dyadic inputs round-trip exactly through decimal quotes.
            _quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
            _chunks = Math.Min(Count - Period, Math.Min(8, Environment.ProcessorCount));
            _segments = Enumerable.Range(0, _chunks).Select(chunk =>
            {
                int start = (Count - Period) * chunk / _chunks;
                int end = (Count - Period) * (chunk + 1) / _chunks;
                return _quotes[start..(end + Period)];
            }).ToArray();
            var native = NativeValues([NativeSingle()]);
            Check(native, NativeValues(NativeParallel()));
            var expected = TrueRangeRatioComparison.VortexReference(_bars, Period, false);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new WindowVortex(Period);
                using var run = Build(indicator, history);
                for (int slot = 0; slot < 2; slot++)
                {
                    for (int i = 0; i < Count; i++)
                        if (BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0) != BitConverter.DoubleToInt64Bits(run[indicator.Outputs[slot]][i])
                            || run[indicator.Outputs[slot + 2]][i] != (expected[slot][i].HasValue ? 1 : 0))
                            throw new InvalidOperationException($"Exact Vortex mismatch at {slot}/{i}.");
                }
                Check(native, [run[indicator.Positive].Slice(Period).ToArray(), run[indicator.Negative].Slice(Period).ToArray()]);
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
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowVortex(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowVortex(Period), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    // Native eager results are retained directly; normalization is setup-only.
    [Benchmark(Baseline = true)] public IEnumerable<VortexResult> NativeSingle() => _quotes.GetVortex(Period);
    [Benchmark] public IEnumerable<VortexResult>[] NativeParallel()
    {
        var results = new IEnumerable<VortexResult>[_chunks];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk => results[chunk] = _segments[chunk].GetVortex(Period));
        return results;
    }

    private double[][] NativeValues(IEnumerable<VortexResult>[] groups)
    {
        var values = new[] { new List<double>(Count - Period), new List<double>(Count - Period) };
        for (int chunk = 0; chunk < groups.Length; chunk++)
        {
            int start = groups.Length == 1 ? 0 : (Count - Period) * chunk / _chunks;
            int end = groups.Length == 1 ? Count - Period : (Count - Period) * (chunk + 1) / _chunks;
            int i = 0;
            foreach (var row in groups[chunk])
            {
                if (i >= end - start + Period || row.Date != _quotes[start + i].Date
                    || row.Pvi.HasValue != (i >= Period) || row.Nvi.HasValue != (i >= Period))
                    throw new InvalidOperationException("Unexpected native Vortex alignment.");
                if (i >= Period) { values[0].Add(row.Pvi!.Value); values[1].Add(row.Nvi!.Value); }
                i++;
            }
            if (i != end - start + Period) throw new InvalidOperationException("Unexpected native Vortex length.");
        }
        return [values[0].ToArray(), values[1].ToArray()];
    }

    private static void Check(double[][] expected, double[][] actual)
    {
        for (int slot = 0; slot < 2; slot++)
        {
            if (expected[slot].Length != actual[slot].Length) throw new InvalidOperationException("Vortex output length mismatch.");
            for (int i = 0; i < expected[slot].Length; i++)
                if (!double.IsFinite(expected[slot][i]) || !double.IsFinite(actual[slot][i])
                    || Math.Abs(expected[slot][i] - actual[slot][i]) > 1e-10 * Math.Max(1, Math.Abs(expected[slot][i])))
                    throw new InvalidOperationException($"Native Vortex mismatch at {slot}/{i}.");
        }
    }
}
