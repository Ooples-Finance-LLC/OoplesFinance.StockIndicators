using System.Numerics;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using Range = System.Range;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSeededEmaBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetDema", "Skender.GetTema", "TaLib.Functions.Dema", "TaLib.Functions.Tema")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _prices = null!;
    private (DateTime, double)[][] _prefixes = null!;
    private int _priorDop;
    private bool Cascaded => Operation.StartsWith("TaLib.", StringComparison.Ordinal);
    private int Order => Operation.EndsWith("Dema", StringComparison.Ordinal) ? 2 : 3;
    private int First => (Period - 1) * (Cascaded ? Order : 1);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var input = SkenderWindowBenchmarkInput.Create(Count, 0);
        _bars = input.Bars; _prices = input.Prices;
        int chunks = Math.Min(8, Environment.ProcessorCount);
        _prefixes = Enumerable.Range(0, chunks).Select(i => input.Tuples[..(Count * (i + 1) / chunks)]).ToArray();
        var expected = Reference(false);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create(); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
        var native = Reference(true);
        Check(NativeSingle(), native); Check(NativeParallel(), native);
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    private SeededExponentialAverage Create() => new(Period, Order,
        Cascaded ? ExponentialSeedMode.Cascaded : ExponentialSeedMode.Shared);
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    [Benchmark(Baseline = true)] public object NativeSingle() => NativePrefix(_prefixes.Length - 1);
    private object NativePrefix(int chunk)
    {
        if (!Cascaded) return Order == 2 ? (object)_prefixes[chunk].GetDema(Period) : _prefixes[chunk].GetTema(Period);
        int end = _prefixes[chunk].Length;
        var output = new double[end];
        Range range;
        var code = Order == 2
            ? Functions.Dema<double>(_prices.AsSpan(0, end), Range.All, output, out range, Period)
            : Functions.Tema<double>(_prices.AsSpan(0, end), Range.All, output, out range, Period);
        if (code != TALib.Core.RetCode.Success || range.Start.Value != First || range.End.Value != end)
            throw new InvalidOperationException("Unexpected seeded EMA packed range.");
        return output;
    }

    [Benchmark] public double[] NativeParallel()
    {
        var output = new double[Count];
        int chunks = _prefixes.Length;
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            // Recursive stages require the entire prefix, including every seed.
            var local = NativePrefix(chunk);
            int start = Count * chunk / chunks, end = _prefixes[chunk].Length;
            if (Cascaded)
            {
                start = Math.Max(start, First);
                ((double[])local).AsSpan(start - First, end - start).CopyTo(output.AsSpan(start - First));
            }
            else for (int i = start; i < end; i++) output[i] = SkenderValue(local, i) ?? double.NaN;
        });
        return output;
    }

    private static double? SkenderValue(object result, int i) => result is IReadOnlyList<DemaResult> dema
        ? dema[i].Dema : ((IReadOnlyList<TemaResult>)result)[i].Tema;

    private void Check(object result, double[] expected)
    {
        if (Cascaded)
        {
            AsinFeasibilityBenchmarks.RequireSame(expected[First..], ((double[])result)[..(Count - First)]);
            return;
        }
        var actual = result as double[] ?? Enumerable.Range(0, Count).Select(i => SkenderValue(result, i) ?? double.NaN).ToArray();
        if (actual.Take(First).Any(v => !double.IsNaN(v))) throw new InvalidOperationException("Unexpected seeded EMA startup presence.");
        AsinFeasibilityBenchmarks.RequireSame(expected[First..], actual[First..]);
    }

    private double[] Reference(bool native)
    {
        var layers = new double[Order][];
        double alpha = Divide(2, Period + 1);
        for (int stage = 0; stage < Order; stage++)
        {
            var values = layers[stage] = new double[Count];
            int first = (Period - 1) * (Cascaded ? stage + 1 : 1);
            BigInteger exactSeed = 0; double nativeSeed = 0;
            for (int i = first - Period + 1; i <= first; i++)
            {
                double value = stage == 0 || !Cascaded ? _prices[i] : layers[stage - 1][i];
                exactSeed += Units(value); nativeSeed = Add(nativeSeed, value);
            }
            values[first] = native ? Divide(nativeSeed, Period) : Round(exactSeed, Period * Grid);
            for (int i = first + 1; i < Count; i++)
            {
                double input = stage == 0 ? _prices[i] : layers[stage - 1][i];
                values[i] = native ? Add(values[i - 1], Multiply(alpha, Subtract(input, values[i - 1])))
                    : Round(2 * Units(input) + (Period - 1) * Units(values[i - 1]), (Period + 1) * Grid);
            }
        }
        var output = new double[Count];
        for (int i = First; i < Count; i++)
            output[i] = native
                ? Order == 2 ? Subtract(Multiply(2, layers[0][i]), layers[1][i])
                    : Add(Subtract(Multiply(3, layers[0][i]), Multiply(3, layers[1][i])), layers[2][i])
                : Round(Order * Units(layers[0][i]) - (Order == 2 ? 1 : 3) * Units(layers[1][i])
                    + (Order == 3 ? Units(layers[2][i]) : BigInteger.Zero), Grid);
        return output;
    }
}
