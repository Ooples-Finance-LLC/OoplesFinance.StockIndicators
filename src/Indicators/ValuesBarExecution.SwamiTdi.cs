#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedSwamiState(int fast, int slow) : IIndicatorState, IDisposable
    {
        private readonly SwamiWindow _window = new(fast, slow);
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.High, bar.Low, true).Line;
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedTradersDynamicState(int rsi, int band, int fast, int slow) : IMultiOutputState, IDisposable
    {
        private readonly TradersDynamicWindow _window = new(MovingAvgType.SimpleMovingAverage, rsi, band, fast, slow);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Upper; outputs[1] = point.Middle; outputs[2] = point.Lower;
            outputs[3] = point.Line; outputs[4] = point.Signal;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
