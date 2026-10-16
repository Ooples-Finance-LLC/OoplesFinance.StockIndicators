using System.Linq.Expressions;
using System.Reflection;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// A/B the ingestion + arithmetic pipelines with identical allocation and data.
// These isolate dispatch changes; public BuildAsync comparisons remain in PilotWorkerScalingBenchmarks.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotSmaDispatchBenchmarks
{
    private static readonly Action<Bar[], double[], Bar[]?> Old = Bind(false);
    private static readonly Action<Bar[], double[], Bar[]?> Fused = Bind(true);
    [Params(false, true)] public bool Full { get; set; }
    [Params(false, true)] public bool Decimal { get; set; }
    private Bar[] _bars = null!;
    private int _previousDop;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        if (Environment.ProcessorCount < 2) throw new InvalidOperationException("Requires parallel CPU execution.");
        var close = Enumerable.Range(0, 100_000).Select(i => Decimal ? 100 + i % 19 / 100d : (i % 127 - 63) / 64d).ToArray();
        _bars = close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var expected = new double[close.Length];
        CpuFeasibilityPrototypes.CurrentSma(close, expected, 20);
        AsinFeasibilityBenchmarks.RequireSame(expected, TwoDispatches());
        AsinFeasibilityBenchmarks.RequireSame(expected, OneDispatch());
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark(Baseline = true)] public double[] TwoDispatches() => Execute(Old);
    [Benchmark] public double[] OneDispatch() => Execute(Fused);

    private double[] Execute(Action<Bar[], double[], Bar[]?> kernel)
    {
        var output = GC.AllocateUninitializedArray<double>(_bars.Length);
        var history = Full ? GC.AllocateUninitializedArray<Bar>(_bars.Length) : null;
        kernel(_bars, output, history);
        return output;
    }

    // Compile fixed diagnostic bindings once outside timing; no reflection invocation
    // or argument boxing occurs inside either measured pipeline.
    private static Action<Bar[], double[], Bar[]?> Bind(bool fused)
    {
        var type = typeof(IIndicator).Assembly.GetType("OoplesFinance.StockIndicators.Indicators.ValuesBarExecution", true)!;
        MethodInfo Method(string name) => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;
        var source = Expression.Parameter(typeof(Bar[]));
        var output = Expression.Parameter(typeof(double[]));
        var history = Expression.Parameter(typeof(Bar[]));
        var period = Expression.Constant(20);
        var cancellation = Expression.Constant(default(CancellationToken));
        Expression body;
        if (fused)
        {
            var latest = Expression.Variable(typeof(Bar));
            var finite = Expression.Variable(typeof(bool));
            var certified = Expression.Variable(typeof(bool));
            body = Expression.Block(new[] { latest, finite, certified },
                Expression.Call(Method("TryExecuteSmaParallel"), source, output, period, cancellation, latest, finite, certified, history),
                Expression.Empty());
        }
        else
        {
            var fill = Method("FillSmaColumn");
            var grid = Expression.Variable(fill.GetParameters()[4].ParameterType.GetElementType()!);
            var positive = Expression.Variable(fill.GetParameters()[5].ParameterType.GetElementType()!);
            var bounded = positive.Type.GetMethod("Certifies", BindingFlags.Instance | BindingFlags.NonPublic)!;
            body = Expression.Block(new[] { grid, positive },
                Expression.Call(fill, source, output, period, cancellation, grid, positive, history),
                Expression.Call(Method("ComputeSma"), output, output, period, grid, cancellation, Expression.Constant(true),
                    Expression.Call(positive, bounded, period),
                    Expression.Condition(Expression.Equal(history, Expression.Constant(null, typeof(Bar[]))), Expression.Constant(8), Expression.Constant(4))),
                Expression.Empty());
        }
        return Expression.Lambda<Action<Bar[], double[], Bar[]?>>(body, source, output, history).Compile();
    }
}
