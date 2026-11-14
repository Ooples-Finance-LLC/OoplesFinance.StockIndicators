#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedMartinRatioState : IIndicatorState, IDisposable
    {
        private readonly MartinWindow? _window;
        internal GeneratedMartinRatioState(int length, double benchmark, int count)
        {
            length = Math.Max(1, length);
            if (length >= count)
            {
                // Before the lag is reached every return and its mean is zero.
                // Keep benchmark validation even when no history storage is needed.
                if (MathHelper.IsValueNullOrInfinity(benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark));
                var target = Math.Pow(1 + benchmark, length / 360d) - 1;
                if (MathHelper.IsValueNullOrInfinity(target)) throw new ArgumentOutOfRangeException(nameof(benchmark), "The period benchmark must be finite and real.");
            }
            else _window = new(MovingAvgType.SimpleMovingAverage, length, benchmark);
        }
        public double Update(in Bar bar) => _window?.Next(bar.Close, true) ?? 0;
        public void Reset() => _window?.Reset();
        public void Dispose() => _window?.Dispose();
    }
}
#endif
