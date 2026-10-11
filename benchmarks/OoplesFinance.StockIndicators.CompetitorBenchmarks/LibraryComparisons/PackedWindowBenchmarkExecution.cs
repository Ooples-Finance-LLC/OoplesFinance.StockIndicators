using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal interface IPackedWindowOperation
{
    static abstract TALib.Core.RetCode Calculate(ReadOnlySpan<double> input, Span<double> output, out System.Range range, int period);
}

internal readonly struct TaSumOperation : IPackedWindowOperation
{
    public static TALib.Core.RetCode Calculate(ReadOnlySpan<double> input, Span<double> output, out System.Range range, int period) =>
        Functions.Sum<double>(input, System.Range.All, output, out range, period);
}

internal readonly struct TaWmaOperation : IPackedWindowOperation
{
    public static TALib.Core.RetCode Calculate(ReadOnlySpan<double> input, Span<double> output, out System.Range range, int period) =>
        Functions.Wma<double>(input, System.Range.All, output, out range, period);
}

internal static class PackedWindowBenchmarkExecution
{
    internal static (double[] Closes, Bar[] Bars) Input(int count)
    {
        var closes = Enumerable.Range(0, count).Select(i => 10 + (i % 101) / 8d).ToArray();
        return (closes, closes.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray());
    }

    internal static void Verify(double[] expected, double[] parallel, double[] startup, Func<IIndicator> create,
        Func<IIndicator, IndicatorHistoryMode, bool, IIndicatorRun> build)
    {
        AsinFeasibilityBenchmarks.RequireSame(expected, parallel);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool gpu in new[] { false, true })
        {
            var indicator = create();
            using var run = build(indicator, mode, gpu);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].Slice(startup.Length).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(startup, run[indicator].Slice(0, startup.Length).ToArray());
        }
    }

    internal static double[] Single<T>(double[] closes, int period) where T : struct, IPackedWindowOperation
    {
        var output = GC.AllocateUninitializedArray<double>(closes.Length - period + 1);
        Calculate<T>(closes, output, period);
        return output;
    }

    internal static double[] Parallel<T>(double[] closes, int period) where T : struct, IPackedWindowOperation
    {
        var output = GC.AllocateUninitializedArray<double>(closes.Length - period + 1);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
            Calculate<T>(closes.AsSpan(start, end - start + period - 1), output.AsSpan(start, end - start), period);
        });
        return output;
    }

    private static void Calculate<T>(ReadOnlySpan<double> input, Span<double> output, int period) where T : struct, IPackedWindowOperation
    {
        var status = T.Calculate(input, output, out var range, period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != period - 1
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Native packed window failed.");
    }

    internal static IIndicatorRun Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode mode, bool gpu)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(mode).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution!.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose(); throw new InvalidOperationException("Required GPU window did not execute on the device.");
        }
        return run;
    }
}
