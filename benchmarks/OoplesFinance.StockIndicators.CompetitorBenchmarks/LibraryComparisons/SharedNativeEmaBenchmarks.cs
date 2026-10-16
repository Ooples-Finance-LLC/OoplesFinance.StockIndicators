using System.Numerics;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedNativeEmaBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Ema", "Skender.GetEma", "QuanTAlib.Dema", "QuanTAlib.Tema", "QuanTAlib.Qema")] public string Operation { get; set; } = "";
    private static readonly double[] QuadrupleAlphas = [.2, .2, .2, .2];
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _inputs = null!;
    private (DateTime, double)[] _tuples = null!;
    private (DateTime, double)[][] _prefixes = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var input = SkenderWindowBenchmarkInput.Create(Count, 0);
        _bars = input.Bars; _tuples = input.Tuples;
        _inputs = input.Prices.Select(v => new QuanTAlib.TValue(default, v, true, false)).ToArray();
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _prefixes = Enumerable.Range(0, chunks).Select(i => _tuples[..(Count * (i + 1) / chunks)]).ToArray();
        int order = CompensatedOrder;
        var (exact, native) = Operation == "QuanTAlib.Qema"
            ? (MassNormalizedComparison.Reference(input.Prices, QuadrupleAlphas, false),
                MassNormalizedComparison.Reference(input.Prices, QuadrupleAlphas, true))
            : order == 0 ? References(input.Prices)
            : (CompensatedAverageComparison.Reference(input.Prices, Period, order, false),
                CompensatedAverageComparison.Reference(input.Prices, Period, order, true));
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create(); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(exact, run[indicator].ToArray());
        }
    }

    private static (double[] Exact, double[] Native) References(double[] prices)
    {
        var exact = new double[prices.Length]; var native = new double[prices.Length];
        BigInteger prefix = 0;
        double alpha = Divide(2, Period + 1);
        for (int i = 0; i < prices.Length; i++)
        {
            var units = Units(prices[i]);
            if (i < Period)
            {
                prefix += units;
                exact[i] = native[i] = Round(prefix, (i + 1) * Grid);
            }
            else
            {
                // Builder rounds the complete convex ratio once; both native
                // sources round alpha, subtraction, multiplication and addition.
                exact[i] = Round(2 * units + (Period - 1) * Units(exact[i - 1]), (Period + 1) * Grid);
                native[i] = Add(native[i - 1], Multiply(alpha, Subtract(prices[i], native[i - 1])));
            }
        }
        return (exact, native);
    }

    private void CheckNative(object output, double[] expected)
    {
        if (output is double[] values) AsinFeasibilityBenchmarks.RequireSame(expected, values);
        else
        {
            var rows = (IReadOnlyList<EmaResult>)output;
            if (rows.Count != Count || rows.Take(Period - 1).Any(r => r.Ema.HasValue)
                || rows.Skip(Period - 1).Any(r => !r.Ema.HasValue))
                throw new InvalidOperationException("Unexpected native EMA presence.");
            AsinFeasibilityBenchmarks.RequireSame(expected.Skip(Period - 1).ToArray(),
                rows.Skip(Period - 1).Select(r => r.Ema!.Value).ToArray());
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    private int CompensatedOrder => Operation == "QuanTAlib.Dema" ? 2 : Operation == "QuanTAlib.Tema" ? 3 : 0;
    private IIndicator Create() => Operation == "QuanTAlib.Qema" ? new MassNormalizedQuadrupleEma()
        : CompensatedOrder == 0 ? new Ema(Period) : new CompensatedExponentialAverage(Period, CompensatedOrder);
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        if (Operation == "Skender.GetEma") return (IReadOnlyList<EmaResult>)_tuples.GetEma(Period);
        var output = GC.AllocateUninitializedArray<double>(Count);
        Quan(0, Count, output);
        return output;
    }
    [Benchmark] public object NativeParallel()
    {
        int chunks = _prefixes.Length;
        if (Operation == "Skender.GetEma")
        {
            var output = new EmaResult[Count];
            CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                var local = (IReadOnlyList<EmaResult>)_prefixes[chunk].GetEma(Period);
                int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
                for (int i = start; i < end; i++) output[i] = local[i];
            });
            return output;
        }
        var values = GC.AllocateUninitializedArray<double>(Count);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            Quan(Count * chunk / chunks, Count * (chunk + 1) / chunks, values));
        return values;
    }
    private void Quan(int start, int end, double[] values)
    {
        QuanTAlib.AbstractBase state = Operation == "QuanTAlib.Qema" ? MassNormalizedComparison.NativeIndicator(Period, QuadrupleAlphas)
            : CompensatedOrder == 0 ? new QuanTAlib.Ema(Period, true)
            : CompensatedAverageComparison.NativeIndicator(Period, CompensatedOrder);
        // Recursive EMA needs its whole prefix, not a finite window overlap.
        for (int i = 0; i < start; i++) state.Calc(_inputs[i]);
        for (int i = start; i < end; i++) values[i] = state.Calc(_inputs[i]).Value;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) =>
        SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}
