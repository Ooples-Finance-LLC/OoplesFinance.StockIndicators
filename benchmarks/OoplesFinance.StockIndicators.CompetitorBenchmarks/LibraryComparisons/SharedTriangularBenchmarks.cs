using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTriangularBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Trima", "TaLib.Functions.Trima")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private double[] _closes = null!;
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _input = null!;
    private int _priorDop;
    internal bool IncludeGpuValidation { get; set; } = true;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        (_closes, _bars) = PackedWindowBenchmarkExecution.Input(Count);
        _input = _closes.Select(v => new QuanTAlib.TValue(v, true, false)).ToArray();
        var expected = new double[Count];
        for (int i = Period - 1; i < Count; i++)
        {
            // The fixture is bounded multiples of 1/8, so these integer-weighted
            // terms and their sum are exactly representable. Round only division.
            double sum = 0;
            for (int j = 0; j < Period; j++)
                sum += _closes[i - Period + 1 + j] * Math.Min(j + 1, Period - j);
            expected[i] = sum / 110;
        }
        var serial = NativeSingle(); var parallel = NativeParallel();
        AsinFeasibilityBenchmarks.RequireSame(serial, parallel);
        // Native normalized coefficients/multiply-by-reciprocal can differ by
        // rounding from our single exact mean. Compare native via its contract.
        var native = Operation.StartsWith("Quan", StringComparison.Ordinal)
            ? serial : new double[Period - 1].Concat(serial).ToArray();
        ComparisonVerifier.Compare(new ComparisonSeries(Period - 1, expected),
            new ComparisonSeries(Period - 1, native), "triangular native");
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool gpu in IncludeGpuValidation ? new[] { false, true } : new[] { false })
        {
            var indicator = new TriangularWindowAverage(Period);
            using var run = Build(indicator, mode, gpu);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new TriangularWindowAverage(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new TriangularWindowAverage(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(new TriangularWindowAverage(Period), IndicatorHistoryMode.Full, true);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(new TriangularWindowAverage(Period), IndicatorHistoryMode.LatestOnly, true);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode mode, bool gpu = false) =>
        PackedWindowBenchmarkExecution.Build(_bars, indicator, mode, gpu);

    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        if (!Operation.StartsWith("Quan", StringComparison.Ordinal))
            return PackedWindowBenchmarkExecution.Single<TriangularOperation>(_closes, Period);
        var output = new double[Count]; Quan(0, Count, output); return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        if (!Operation.StartsWith("Quan", StringComparison.Ordinal))
            return PackedWindowBenchmarkExecution.Parallel<TriangularOperation>(_closes, Period);
        var output = new double[Count];
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks,
            c => Quan(Count * c / chunks, Count * (c + 1) / chunks, output));
        return output;
    }
    private void Quan(int start, int end, double[] output)
    {
        var state = new QuanTAlib.Trima(Period);
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_input[i]);
        for (int i = start; i < end; i++) output[i] = state.Calc(_input[i]).Value;
    }
    private readonly struct TriangularOperation : IPackedWindowOperation
    {
        public static TALib.Core.RetCode Calculate(ReadOnlySpan<double> input, Span<double> output, out System.Range range, int period) =>
            TALib.Functions.Trima<double>(input, System.Range.All, output, out range, period);
    }
}
