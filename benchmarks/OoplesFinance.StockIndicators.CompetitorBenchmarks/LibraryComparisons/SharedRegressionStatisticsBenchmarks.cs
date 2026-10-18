using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRegressionStatisticsBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Slope", "Skender.GetSlope")] public string Operation { get; set; } = "QuanTAlib.Slope";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private (DateTime, double)[] _input = null!;
    private QuanTAlib.TValue[] _quan = null!;
    private int _priorDop;
    // Pack retained scalar outputs with one presence mask instead of charging
    // the native streaming API for boxed nullable results or per-row objects.
    public readonly record struct NativePoint(double Slope, double Intercept, double StdDev,
        double RSquared, double Line, byte Presence);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        if (Operation is not ("QuanTAlib.Slope" or "Skender.GetSlope")) throw new ArgumentException("Unknown regression operation.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        var data = CompetitorData.FromCloses(prices);
        _bars = data.IndicatorBars;
        _input = _bars.Select(b => (b.Time, b.Close)).ToArray();
        _quan = _bars.Select(b => new QuanTAlib.TValue(b.Time, b.Close, true)).ToArray();
        var pair = Operation == "Skender.GetSlope" ? RegressionStatisticsComparison.SnapshotPair : RegressionStatisticsComparison.Pair;
        var expected = pair.Reference!(data, Period);
        var nativeExpected = (pair.CompetitorReference ?? pair.Reference)!(data, Period);
        var serialResult = NativeSingle();
        var parallelResult = NativeParallel();
        var serial = Columns(serialResult);
        CheckReference(nativeExpected, serial);
        RequireSame(serial, Columns(parallelResult));
        if (serialResult is IEnumerable<SlopeResult> nativeRows && parallelResult is IEnumerable<SlopeResult> parallelRows
            && !nativeRows.Select(r => (r.Date, r.Line)).SequenceEqual(parallelRows.Select(r => (r.Date, r.Line))))
            throw new InvalidOperationException("Native regression dates or decimal line values differ.");
        if (Operation == "Skender.GetSlope")
            RequireSame(serial, Columns(data.Quotes.GetSlope(Period)));
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            var columns = Enumerable.Range(0, 5).Select(slot =>
            {
                var values = run[indicator.Outputs[slot]].ToArray();
                var present = run[indicator.Outputs[slot + 5]].ToArray();
                for (int i = 0; i < values.Length; i++)
                    if (present[i] is not (0 or 1) || present[i] == 0 && values[i] != 0)
                        throw new InvalidOperationException("Builder regression presence or missing placeholder differs.");
                return values.Select((value, i) => present[i] > 0 ? (double?)value : null).ToArray();
            }).ToArray();
            CheckReference(expected, columns);
        }
    }

    private static void CheckReference(ComparisonSeries expected, double?[][] actual)
    {
        for (int slot = 0; slot < 5; slot++)
        {
            var column = expected.Outputs[RegressionStatisticsComparison.Names[slot]];
            if (column.Values.Length != actual[slot].Length) throw new InvalidOperationException("Regression length differs.");
            for (int i = 0; i < column.Values.Length; i++)
            {
                bool present = column.Present![i];
                if (present != actual[slot][i].HasValue || present &&
                    (!double.IsFinite(actual[slot][i]!.Value) || Math.Abs(column.Values[i] - actual[slot][i]!.Value) > 1e-8))
                    throw new InvalidOperationException($"Regression independent reference differs at slot {slot}, row {i}.");
            }
        }
    }
    private static void RequireSame(double?[][] expected, double?[][] actual)
    {
        for (int slot = 0; slot < 5; slot++)
        {
            if (expected[slot].Length != actual[slot].Length) throw new InvalidOperationException("Regression length differs.");
            for (int i = 0; i < expected[slot].Length; i++)
                if (expected[slot][i].HasValue != actual[slot][i].HasValue || expected[slot][i] is double value &&
                    BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(actual[slot][i]!.Value))
                    throw new InvalidOperationException($"Regression native replay differs at slot {slot}, row {i}.");
        }
    }
    private static double?[][] Columns(object result)
    {
        if (result is IEnumerable<SlopeResult> rows)
        {
            var values = rows.ToArray();
            return [values.Select(r => r.Slope).ToArray(), values.Select(r => r.Intercept).ToArray(),
                values.Select(r => r.StdDev).ToArray(), values.Select(r => r.RSquared).ToArray(),
                values.Select(r => (double?)r.Line).ToArray()];
        }
        var points = (NativePoint[])result;
        return Enumerable.Range(0, 5).Select(slot => points.Select(r => (r.Presence & (1 << slot)) == 0 ? (double?)null
            : slot switch { 0 => r.Slope, 1 => r.Intercept, 2 => r.StdDev, 3 => r.RSquared, _ => r.Line }).ToArray()).ToArray();
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        // The public tuple overload eagerly returns the native collection.
        if (Operation == "Skender.GetSlope") return _input.GetSlope(Period);
        var result = new NativePoint[Count];
        CalculateQuan(0, Count, result);
        return result;
    }
    [Benchmark] public object NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        if (Operation == "QuanTAlib.Slope")
        {
            var result = new NativePoint[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
                CalculateQuan(Count * worker / workers, Count * (worker + 1) / workers, result));
            return result;
        }
        var rows = new SlopeResult[Count];
        var overlay = new decimal?[Math.Min(Period, Count)];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            // Prefixes preserve the public API's global x coordinates and exact
            // floating operation order. Only the final prefix supplies Line.
            int index = 0;
            foreach (var row in _input.Take(end).GetSlope(Period))
            {
                if (end == Count && Count >= Period && index >= Count - Period)
                    overlay[index - (Count - Period)] = row.Line;
                if (index >= start) { row.Line = null; rows[index] = row; }
                index++;
            }
        });
        if (Count >= Period)
            for (int i = 0; i < Period; i++) rows[Count - Period + i].Line = overlay[i];
        return rows;
    }
    private void CalculateQuan(int start, int end, NativePoint[] result)
    {
        var state = new QuanTAlib.Slope(Period);
        // Every complete window on this varying-price fixture has nonzero
        // variance: P-1 seed bars suffice, including its retained R-squared.
        // Setup compares all fields and presence bit-for-bit to serial results.
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_quan[i]);
        for (int i = start; i < end; i++)
        {
            double slope = state.Calc(_quan[i]).Value;
            byte presence = (byte)(1 | (state.Intercept.HasValue ? 2 : 0) | (state.StdDev.HasValue ? 4 : 0)
                | (state.RSquared.HasValue ? 8 : 0) | (state.Line.HasValue ? 16 : 0));
            result[i] = new(slope, state.Intercept ?? 0, state.StdDev ?? 0, state.RSquared ?? 0, state.Line ?? 0, presence);
        }
    }
    internal IIndicator Create() => Operation == "Skender.GetSlope" ? new RegressionWithFinalLine(Period) : new WindowRegressionStatistics(Period);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        { run.Dispose(); throw new InvalidOperationException("Required GPU regression did not execute on the device."); }
        return run;
    }
}
