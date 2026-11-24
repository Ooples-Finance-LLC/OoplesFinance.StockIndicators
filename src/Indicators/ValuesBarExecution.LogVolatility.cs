#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private struct LogVolatilityKernel(WindowLogVolatility.State state, BoundedOutput target)
        : IBoundedKernel<LogVolatilityKernel>
    {
        private int _index = target.Origin;
        public static LogVolatilityKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var owner = (WindowLogVolatility)indicator;
            return new(new(owner.Period, owner.Realized, owner.Annualized), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
            {
                target.Values[1][index] = values[1];
                target.Values[2][index] = bar.Close;
            }
            return values[0];
        }
    }

    private static bool TryFillLogVolatilityRegions(Bar[] source, double[][] output,
        WindowLogVolatility owner, CancellationToken cancellation, OwnedBarBuffer? owned, out Bar latest)
    {
        latest = default;
        // P returns need P preceding prices. Bound replay work and retain the
        // original lazy state for short batches, long periods and serial hosts.
        if (owner.Period >= 4096 || source.Length < 64L * (owner.Period + 1)
            || !CanParallelize(source.Length, BoundedParallelMinimum(owner)) || WorkerCount() < 2)
            return false;
        cancellation.ThrowIfCancellationRequested();
        var closes = new double[source.Length];
        double[][] columns = [output[0], output[1], closes];
        latest = FillBoundedWindow<LogVolatilityKernel>(source, columns, owner,
            owner.Period + 1, cancellation, owned);
        // Inspect the closes actually consumed by each worker. A separate scan
        // of the caller's mutable bars could certify different input/history.
        foreach (double close in closes)
        {
            cancellation.ThrowIfCancellationRequested();
            if (close > 0) continue;
            ReplayLogVolatilityCloses(closes, output, owner, cancellation);
            break;
        }
        return true;
    }

    private static void ReplayLogVolatilityCloses(double[] closes, double[][] output,
        WindowLogVolatility owner, CancellationToken cancellation)
    {
        // Zero prices skip returns and Realized poisoning never expires.
        // Unsupported input therefore replays the original whole-series state.
        // This state reads only Close; raw-bar validation/history already ran.
        var state = new WindowLogVolatility.State(owner.Period, owner.Realized, owner.Annualized);
        Span<double> values = stackalloc double[2];
        for (int i = 0; i < closes.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var bar = new Bar(default, 0, 0, 0, closes[i], 0);
            state.Update(in bar, values);
            output[0][i] = values[0];
            output[1][i] = values[1];
        }
    }
}
#endif
