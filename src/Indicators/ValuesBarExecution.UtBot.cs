#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedUtBotState : IMultiOutputState, IDisposable
    {
        private readonly UtBotWindow _window;
        public GeneratedUtBotState(int length, double factor) =>
            _window = new(MovingAvgType.WildersSmoothingMethod, length, factor);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.High, bar.Low, bar.Close, true);
            outputs[0] = point.Stop; outputs[1] = point.Position;
            outputs[2] = point.Buy; outputs[3] = point.Sell;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
