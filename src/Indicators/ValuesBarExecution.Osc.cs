#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedOscState : IIndicatorState, IDisposable
    {
        private readonly StrengthAverage _short, _long;
        public GeneratedOscState(int length, int count)
        {
            length = Math.Max(1, length);
            _short = new(MovingAvgType.SimpleMovingAverage, Math.Max(1, length / 2), count);
            _long = new(MovingAvgType.SimpleMovingAverage, length, count);
        }
        public double Update(in Bar bar) => _long.Next(new StrengthValue(bar.Close), true).Mantissa
            - _short.Next(new StrengthValue(bar.Close), true).Mantissa;
        public void Reset() { _short.Reset(); _long.Reset(); }
        public void Dispose() { _short.Dispose(); _long.Dispose(); }
    }
}
#endif
