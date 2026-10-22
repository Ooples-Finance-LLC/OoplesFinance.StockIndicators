#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedTechnicalRatingsState(int aoFirst, int aoSecond, int rsi, int stochastic, int smooth, int slow) : IMultiOutputState, IDisposable
    {
        private readonly TechnicalRatingsState _window = new(MovingAvgType.ExponentialMovingAverage,
            aoFirst, aoSecond, rsi, stochastic, smooth, slow);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var native = new OhlcvBar("VALUES", BarTimeframe.Minutes(1), bar.Time, bar.Time,
                bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
            var point = _window.NextValues(native, true);
            outputs[0] = point.Total; outputs[1] = point.Oscillator; outputs[2] = point.Averages;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
