using System.Linq.Expressions;
using System.Reflection;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Diagnostic ablation only. Internal delegates are bound once outside measurement,
// without adding benchmark access or new APIs to the production assembly.
internal sealed class CpuBuilderAbWorkload
{
    private delegate void SmaCore(ReadOnlySpan<double> input, Span<double> output, int length);
    private readonly CpuNativeWorkload _work;
    private readonly Func<IReadOnlyList<Bar>, IIndicator, double[][]>? _engine;
    private readonly SmaCore? _sma;

    internal CpuBuilderAbWorkload(string id, int count)
    {
        if (id is not ("TaLib.Functions.Asin" or "TaLib.Functions.Sma" or "TaLib.Candles.RickshawMan"))
            throw new NotSupportedException("A/B diagnosis currently covers the three losing TA-Lib families.");
        _work = new CpuNativeWorkload(id, count, commonGrid: true);
        var assembly = typeof(IIndicator).Assembly;
        if (id == "TaLib.Functions.Sma")
        {
            _sma = assembly.GetType("OoplesFinance.StockIndicators.Core.MovingAverageCore", true)!
                .GetMethod("SimpleMovingAverage", BindingFlags.Static | BindingFlags.NonPublic)!
                .CreateDelegate<SmaCore>();
        }
        else
        {
            var type = assembly.GetType("OoplesFinance.StockIndicators.Indicators.CustomIndicatorEngine", true)!;
            var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            var bars = Expression.Parameter(typeof(IReadOnlyList<Bar>), "bars");
            var indicator = Expression.Parameter(typeof(IIndicator), "indicator");
            Func<IIndicator, double[][]?> resolver = _ => null;
            var arguments = ctor.GetParameters().Select(p => Expression.Default(p.ParameterType)).Cast<Expression>().ToArray();
            arguments[0] = bars;
            arguments[1] = Expression.Constant(resolver);
            // false: the direct engine must validate its inputs itself.
            var call = Expression.Call(Expression.New(ctor, arguments),
                type.GetMethod("Compute", BindingFlags.Instance | BindingFlags.NonPublic)!, indicator);
            _engine = Expression.Lambda<Func<IReadOnlyList<Bar>, IIndicator, double[][]>>(call, bars, indicator).Compile();
        }
        // Verify every materialized engine output, including presence flags and warmup.
        var spec = CpuBuilderWorkload.Create(id);
        using var run = CpuBuilderWorkload.Build(_work, spec).GetAwaiter().GetResult();
        var direct = ComputeOwned();
        if (direct.Length != spec.Outputs.Count) throw new InvalidOperationException("A/B slot mismatch.");
        for (var slot = 0; slot < direct.Length; slot++)
        {
            var expected = run[spec.Outputs[slot]];
            if (direct[slot].Length != expected.Length) throw new InvalidOperationException("A/B length mismatch.");
            for (var i = 0; i < expected.Length; i++)
                if (!direct[slot][i].Equals(expected[i]))
                    throw new InvalidOperationException($"A/B output mismatch at {slot}/{i}.");
        }
        var kernelId = id == "TaLib.Functions.Sma" ? "Trady.Indicator.SimpleMovingAverage" : id;
        var kernel = CpuKernelPilots.Create(kernelId);
        CpuKernelPilots.Verify(kernelId, _work.Data, kernel, new double[count * kernel.OutputCount]);
    }

    internal double[][] ComputeOwned()
    {
        if (_sma is null) return _engine!(_work.Data.IndicatorBars, CpuBuilderWorkload.Create(_work.PairId));
        var output = new double[_work.Data.Count];
        _sma(_work.Data.Closes, output, 20);
        return [output];
    }

    internal double[] KernelOwned()
    {
        // Keep the full OHLCV kernel input here, including for Asin.
        var id = _work.PairId == "TaLib.Functions.Sma" ? "Trady.Indicator.SimpleMovingAverage" : _work.PairId;
        var kernel = CpuKernelPilots.Create(id);
        var output = new double[_work.Data.Count * kernel.OutputCount];
        kernel.Process(_work.Data.IndicatorBars, output);
        return output;
    }
}
