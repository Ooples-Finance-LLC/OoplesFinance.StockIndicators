using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedCurvatureBenchmarks
{
    private const int Period = 20;
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;
    public readonly record struct NativePoint(double Slope, double Intercept, double StdDev,
        double RSquared, double Line, byte Presence);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        var data = CompetitorData.FromCloses(prices);
        _bars = data.IndicatorBars;
        _input = _bars.Select(b => new QuanTAlib.TValue(b.Time, b.Close, true)).ToArray();
        var serial = Columns(NativeSingle());
        CheckReference(CurvatureComparison.Reference(prices, Period, true), serial);
        RequireSame(serial, Columns(NativeParallel()));
        var expected = CurvatureComparison.Reference(prices, Period, false);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var root = Create();
            using var run = Build(root, history);
            var outputs = CurvatureComparison.Names.Select((name, slot) =>
            {
                var values = run[root.Outputs[slot]].ToArray();
                var flags = run[root.Outputs[slot + 5]].ToArray();
                for (int i = 0; i < Count; i++)
                    if (flags[i] is not (0 or 1) || flags[i] == 0 && values[i] != 0)
                        throw new InvalidOperationException("Curvature presence or placeholder differs.");
                return new KeyValuePair<string, ComparisonOutput>(name,
                    new(0, values.Select((v, i) => flags[i] == 1 ? v : double.NaN).ToArray(), flags.Select(v => v == 1).ToArray()));
            }).ToDictionary(x => x.Key, x => x.Value);
            ComparisonVerifier.Compare(expected, new ComparisonSeries(outputs), "shared curvature",
                OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget.Exact);
        }
    }
    private static void CheckReference(ComparisonSeries expected, double?[][] actual)
    {
        for (int slot = 0; slot < 5; slot++)
        {
            var column = expected.Outputs[CurvatureComparison.Names[slot]];
            if (column.Values.Length != actual[slot].Length) throw new InvalidOperationException("Curvature length differs.");
            for (int i = 0; i < column.Values.Length; i++)
            {
                bool present = column.Present![i];
                if (present != actual[slot][i].HasValue || present &&
                    (!double.IsFinite(actual[slot][i]!.Value) || BitConverter.DoubleToInt64Bits(column.Values[i])
                        != BitConverter.DoubleToInt64Bits(actual[slot][i]!.Value)))
                    throw new InvalidOperationException($"Curvature independent reference differs at slot {slot}, row {i}.");
            }
        }
    }
    private static void RequireSame(double?[][] expected, double?[][] actual)
    {
        for (int slot = 0; slot < 5; slot++)
        {
            if (expected[slot].Length != actual[slot].Length) throw new InvalidOperationException("Curvature length differs.");
            for (int i = 0; i < expected[slot].Length; i++)
                if (expected[slot][i].HasValue != actual[slot][i].HasValue || expected[slot][i] is double value &&
                    BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(actual[slot][i]!.Value))
                    throw new InvalidOperationException($"Curvature native replay differs at slot {slot}, row {i}.");
        }
    }

    private static double?[][] Columns(NativePoint[] points) => Enumerable.Range(0, 5)
        .Select(slot => points.Select(r => (r.Presence & (1 << slot)) == 0 ? (double?)null
            : slot switch { 0 => r.Slope, 1 => r.Intercept, 2 => r.StdDev, 3 => r.RSquared, _ => r.Line }).ToArray()).ToArray();
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public NativePoint[] NativeSingle()
    {
        var result = new NativePoint[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public NativePoint[] NativeParallel()
    {
        var result = new NativePoint[Count];
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
        return result;
    }
    private void Calculate(int start, int end, NativePoint[] result)
    {
        var state = new QuanTAlib.Curvature(Period);
        // Two P-1 overlaps preserve the rounded first-stage slopes. This
        // varying fixture has no flat full outer windows; Setup validates
        // retained native correlation and all fields against serial bitwise.
        for (int i = Math.Max(0, start - 2 * (Period - 1)); i < end; i++)
        {
            double value = state.Calc(_input[i]).Value;
            if (i < start) continue;
            byte presence = (byte)(1 | (state.Intercept.HasValue ? 2 : 0) | (state.StdDev.HasValue ? 4 : 0)
                | (state.RSquared.HasValue ? 8 : 0) | (state.Line.HasValue ? 16 : 0));
            result[i] = new(value, state.Intercept ?? 0, state.StdDev ?? 0, state.RSquared ?? 0, state.Line ?? 0, presence);
        }
    }
    private static WindowRegressionStatistics Create()
    {
        var root = new WindowRegressionStatistics(Period);
        root.Of(new WindowLinearRegression(Period, WindowRegressionOutput.Slope));
        return root;
    }
    private IIndicatorRun Build(IIndicator root, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(root).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}
