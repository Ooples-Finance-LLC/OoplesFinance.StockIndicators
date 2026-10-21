#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedShinoharaState(int length, int count, bool second) : IIndicatorState, IDisposable
    {
        // A period larger than the batch never evicts; these ratios have no
        // period-dependent normalization beyond the observed rolling sums.
        private readonly ShinoharaWindow _window = new(Math.Max(1, Math.Min(length, count)));
        public double Update(in Bar bar)
        {
            var point = _window.Next(second ? 0 : bar.Open, bar.High, bar.Low, second ? bar.Close : 0, true);
            return second ? point.B : point.A;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
