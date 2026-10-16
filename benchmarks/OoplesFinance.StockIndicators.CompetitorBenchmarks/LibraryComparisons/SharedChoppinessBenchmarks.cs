using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedChoppinessBenchmarks
{
    private const int Period = 14;
    [Params("Skender.GetChop")] public string Operation { get; set; } = "Skender.GetChop";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private Quote[] _quotes = null!;
    private Quote[][] _segments = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "Skender.GetChop") throw new ArgumentException("Unsupported Choppiness operation.");
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
            var native = NativeValues([(IEnumerable<ChopResult>)NativeSingle()]);
            var nativeReference = ChoppinessComparison.NativeReference(_bars, Period);
            for (int i = Period; i < Count; i++)
            {
                double reference = nativeReference[i] ?? throw new InvalidOperationException("Native Choppiness reference is absent.");
                if (!double.IsFinite(reference) || !double.IsFinite(native[i - Period])
                    || Math.Abs(reference - native[i - Period]) > 4e-15 * Math.Abs(reference))
                    throw new InvalidOperationException($"Native Choppiness/reference mismatch at {i}.");
            }
            AsinFeasibilityBenchmarks.RequireSame(native, NativeValues(NativeParallel()));
            var expected = ChoppinessComparison.Reference(_bars, Period);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new WindowChoppinessIndex(Period);
                using var run = Build(indicator, history);
                for (int i = 0; i < Count; i++)
                {
                    double value = expected[i] ?? 0;
                    double actual = run[indicator.Value][i];
                    if (!double.IsFinite(actual) || Math.Abs(value - actual) > 4e-15 * Math.Abs(value)
                        || run[indicator.IsDefined][i] != (expected[i].HasValue ? 1 : 0)) // NOSONAR: S1244 - Presence columns are discrete 0/1 flags, not approximate measurements.
                        throw new InvalidOperationException($"Choppiness builder/reference mismatch at {i}.");
                }
                Check(native, run[indicator.Value].Slice(Period).ToArray());
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
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowChoppinessIndex(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowChoppinessIndex(Period), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);

    // Native eager results are retained directly; normalization is setup-only.
    [Benchmark(Baseline = true)] public object NativeSingle() => _quotes.GetChop(Period);
    [Benchmark] public IEnumerable<ChopResult>[] NativeParallel()
    {
        var results = new IEnumerable<ChopResult>[_chunks];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk => results[chunk] = _segments[chunk].GetChop(Period));
        return results;
    }

    private double[] NativeValues(IEnumerable<ChopResult>[] groups)
    {
        var values = new List<double>(Count - Period);
        for (int chunk = 0; chunk < groups.Length; chunk++)
        {
            int start = groups.Length == 1 ? 0 : (Count - Period) * chunk / _chunks;
            int end = groups.Length == 1 ? Count - Period : (Count - Period) * (chunk + 1) / _chunks;
            int i = 0;
            foreach (var row in groups[chunk])
            {
                if (i >= end - start + Period || row.Date != _quotes[start + i].Date
                    || row.Chop.HasValue != (i >= Period))
                    throw new InvalidOperationException("Unexpected native Choppiness alignment.");
                if (i >= Period) values.Add(row.Chop!.Value);
                i++;
            }
            if (i != end - start + Period) throw new InvalidOperationException("Unexpected native Choppiness length.");
        }
        return values.ToArray();
    }

    private static void Check(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Choppiness output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(expected[i]) || !double.IsFinite(actual[i])
                || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Native Choppiness mismatch at {i}.");
    }
}
