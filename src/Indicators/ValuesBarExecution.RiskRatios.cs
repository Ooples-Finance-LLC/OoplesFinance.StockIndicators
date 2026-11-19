#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRiskRatioState : IIndicatorState, IDisposable
    {
        private readonly ReturnScoreWindow? _score;
        private readonly SortinoWindow? _sortino;
        internal GeneratedRiskRatioState(int length, double benchmark, int count, int kind)
        {
            length = Math.Max(1, length);
            if (length >= count)
            {
                // The lag is never reached, so both variance and downside remain zero.
                // Preserve constructor rejection before omitting unused history storage.
                if (MathHelper.IsValueNullOrInfinity(benchmark)) throw new ArgumentOutOfRangeException(nameof(benchmark));
                var target = Math.Pow(1 + benchmark, length / 360d) - 1;
                if (MathHelper.IsValueNullOrInfinity(target)) throw new ArgumentOutOfRangeException(nameof(benchmark), "The period benchmark must be finite and real.");
            }
            else if (kind == 2) _sortino = new(MovingAvgType.SimpleMovingAverage, length, benchmark);
            else _score = new(MovingAvgType.SimpleMovingAverage, length, benchmark, kind == 0);
        }
        public double Update(in Bar bar) => _score?.Next(bar.Close, true) ?? _sortino?.Next(bar.Close, true) ?? 0;
        public void Reset() { _score?.Reset(); _sortino?.Reset(); }
        public void Dispose() { _score?.Dispose(); _sortino?.Dispose(); }
    }
}
#endif
