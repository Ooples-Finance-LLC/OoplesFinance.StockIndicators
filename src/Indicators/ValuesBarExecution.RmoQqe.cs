#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRmoState(int first, int range, int swing, int output) : IMultiOutputState, IDisposable
    {
        private readonly RmoWindow _window = new(first, range, swing, output);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Rmo; outputs[1] = point.Swing1; outputs[2] = point.Swing2; outputs[3] = point.Swing3;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedQqeState(int length, int smooth, double fast, double slow) : IMultiOutputState, IDisposable
    {
        private readonly QqeWindow _window = new(MovingAvgType.ExponentialMovingAverage, length, smooth, fast, slow);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _window.Next(bar.Close, true);
            outputs[0] = point.Fast; outputs[1] = point.Slow;
        }
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
