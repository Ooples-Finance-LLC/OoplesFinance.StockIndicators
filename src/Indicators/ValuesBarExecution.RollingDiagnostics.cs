#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static bool IsRollingDiagnostics(IIndicator indicator) => indicator is
        WindowMeanAbsoluteDeviation or MovingAverageDiagnostics or WindowBetaStatistics { Period: < int.MaxValue };

    private static Bar FillRollingDiagnostics(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned) => indicator switch
    {
        WindowMeanAbsoluteDeviation deviation => FillBoundedWindow<MeanAbsoluteErrorKernel>(source, output, indicator, deviation.Period, cancellation, owned),
        MovingAverageDiagnostics diagnostics => FillBoundedWindow<MeanDiagnosticsKernel>(source, output, indicator, diagnostics.Period, cancellation, owned),
        // Each of the P returns needs its preceding price: replay P prices.
        WindowBetaStatistics beta => FillBoundedWindow<BetaStatisticsKernel>(source, output, indicator, beta.Period + 1, cancellation, owned),
        _ => throw new InvalidOperationException("Unqualified rolling diagnostics kernel.")
    };

    private readonly struct MeanAbsoluteErrorKernel(WindowMeanAbsoluteDeviation.State state) : IBoundedKernel<MeanAbsoluteErrorKernel>
    {
        public static MeanAbsoluteErrorKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private struct MeanDiagnosticsKernel(MovingAverageDiagnostics.State state, BoundedOutput target) : IBoundedKernel<MeanDiagnosticsKernel>
    {
        private int _index = target.Origin;
        public static MeanDiagnosticsKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[8];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }

    private struct BetaStatisticsKernel(WindowBetaStatistics.State state, BoundedOutput target) : IBoundedKernel<BetaStatisticsKernel>
    {
        private int _index = target.Origin;
        public static BetaStatisticsKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var beta = (WindowBetaStatistics)indicator;
            return new(new(beta.Period, beta.Selection, beta.Market, beta.Evaluation), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[14];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }
}
#endif
