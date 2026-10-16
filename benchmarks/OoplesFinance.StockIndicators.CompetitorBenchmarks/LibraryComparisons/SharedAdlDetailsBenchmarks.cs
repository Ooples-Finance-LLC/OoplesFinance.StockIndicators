using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedAdlDetailsBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetAdl")] public string Operation { get; set; } = "Skender.GetAdl";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Quote[] _quotes = null!;
    private Bar[] _bars = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "Skender.GetAdl") throw new ArgumentException("Unsupported detailed ADL operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            // Common dyadic prices/volumes are exactly convertible to native
            // decimal quotes. Zero ranges, signed flows and average startup vary.
            _quotes = Enumerable.Range(0, Count).Select(i =>
            {
                decimal low = 10 + (i * 13 % 101) / 8m;
                decimal range = i % 31 == 0 ? 0 : 1 << (i % 3);
                decimal close = low + range * (i % 9) / 8m;
                return new Quote { Date = DateTime.UnixEpoch.AddMinutes(i), Open = low,
                    High = low + range, Low = low, Close = close, Volume = i % 13 + 1 };
            }).ToArray();
            _bars = _quotes.Select(q => new Bar(q.Date, (double)q.Open, (double)q.High,
                (double)q.Low, (double)q.Close, (double)q.Volume)).ToArray();
            _chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
            var native = Columns(NativeSingle());
            var parallel = Columns(NativeParallel());
            for (int slot = 0; slot < 5; slot++) Check(native[slot], parallel[slot]);
            var indicator = new AccumulationDistributionWithAverage(Period);
            using var reference = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
                .ConfigureIndicators(indicator).ConfigureBehavior(_ => { }).BuildAsync().GetAwaiter().GetResult();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = Build(indicator, history);
                for (int slot = 0; slot < 5; slot++)
                {
                    var values = actual[indicator.Outputs[slot]].ToArray();
                    Check(reference[indicator.Outputs[slot]].ToArray(), values);
                    Check(native[slot], values);
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
    // GetAdl eagerly returns its native collection; no normalization copy is timed.
    [Benchmark(Baseline = true)] public object NativeSingle() => _quotes.GetAdl(Period);
    [Benchmark]
    public object NativeParallel()
    {
        var result = new AdlResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            // Replay complete prefixes so the cumulative total and its average
            // preserve native rounding. Replay and publication are measured.
            int index = 0;
            foreach (var row in _quotes.Take(end).GetAdl(Period))
            {
                if (index >= start) result[index] = row;
                index++;
            }
        });
        return result;
    }

    internal IIndicatorRun Build(AccumulationDistributionWithAverage indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        return SharedBenchmarkSupport.BuildChecked(builder, gpu, "Detailed ADL GPU fixture did not execute on device.");
    }

    private double[][] Columns(object result)
    {
        if (result is not IReadOnlyList<AdlResult> rows || rows.Count != Count)
            throw new InvalidOperationException("Native ADL must return a complete eager collection.");
        var columns = Enumerable.Range(0, 5).Select(_ => new double[Count]).ToArray();
        for (int i = 0; i < Count; i++)
        {
            if (rows[i].Date != _quotes[i].Date || rows[i].AdlSma.HasValue != (i >= Period - 1))
                throw new InvalidOperationException("Native ADL alignment differs.");
            columns[0][i] = rows[i].MoneyFlowMultiplier ?? throw new InvalidOperationException("Missing multiplier.");
            columns[1][i] = rows[i].MoneyFlowVolume ?? throw new InvalidOperationException("Missing flow.");
            columns[2][i] = rows[i].Adl;
            columns[3][i] = rows[i].AdlSma ?? 0;
            columns[4][i] = rows[i].AdlSma.HasValue ? 1 : 0;
        }
        return columns;
    }

    private static void Check(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("ADL column length differs.");
        for (int i = 0; i < expected.Length; i++)
            if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i]))
                throw new InvalidOperationException($"ADL column differs at {i}.");
    }
}
