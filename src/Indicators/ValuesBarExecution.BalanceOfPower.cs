#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private readonly struct BalanceOfPowerKernel : IPointwiseKernel
    {
        public bool AlwaysDefined => true;
        public bool CanOverflow => true;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            return RoundedBalanceOfPower.Of(bar.Open, bar.High, bar.Low, bar.Close);
        }
    }

    private static Bar FillBalanceOfPower(Bar[] source, double[][] output, BalanceOfPower indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        // The second output is a signal, not a presence channel. Compute the raw
        // ratio through the parallel pointwise engine, then smooth its owned array.
        var latest = FillPointwiseKernel(source, new[] { output[0] }, indicator,
            new BalanceOfPowerKernel(), cancellation, owned);
        if (indicator.Length <= 1)
        {
            // EMA(1) returns its input after startup. Its first exact mean alone
            // canonicalizes negative zero; otherwise both immutable outputs share.
            output[1] = output[0];
            if (output[0].Length > 0 && BitConverter.DoubleToInt64Bits(output[0][0]) == long.MinValue)
            {
                output[1] = (double[])output[0].Clone();
                output[1][0] = 0;
            }
            return latest;
        }
        using var smoother = MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, Math.Max(1, indicator.Length));
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            output[1][i] = smoother.Next(output[0][i], true);
        }
        return latest;
    }

    // Default generated BOP uses EMA. Explicit components and composed inputs
    // retain their existing graph path, just like the other generated routes.
    private sealed class BalanceOfPowerValueState(int period) : IMultiOutputState, IDisposable
    {
        private readonly IMovingAverageSmoother _signal =
            MovingAverageSmootherFactory.Create(MovingAvgType.ExponentialMovingAverage, period);
        public void Reset() => _signal.Reset();
        public void Update(in Bar bar, Span<double> output)
        {
            output[0] = RoundedBalanceOfPower.Of(bar.Open, bar.High, bar.Low, bar.Close);
            output[1] = double.IsInfinity(output[0]) ? double.NaN : _signal.Next(output[0], true);
        }
        public void Dispose() => _signal.Dispose();
    }
}
#endif
