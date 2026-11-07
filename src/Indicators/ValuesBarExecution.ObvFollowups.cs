#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedObvFollowupState : IMultiOutputState, IDisposable
    {
        private readonly ModifiedObvWindow? _modified;
        private readonly ObvReflexWindow? _reflex;
        public GeneratedObvFollowupState(int length, int signal, bool reflex)
        {
            if (reflex) _reflex = new(MovingAvgType.SimpleMovingAverage, length, signal);
            else _modified = new(MovingAvgType.ExponentialMovingAverage, length, signal);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_modified is not null)
            {
                var point = _modified.Next(bar.Close, bar.Volume, true);
                outputs[0] = point.Line; outputs[1] = point.Signal;
            }
            else
            {
                var point = _reflex!.Next(bar.Close, bar.Volume, true);
                outputs[0] = point.Line.Publish(); outputs[1] = point.SignalLine.Publish();
            }
        }
        public void Reset() { _modified?.Reset(); _reflex?.Reset(); }
        public void Dispose() { _modified?.Dispose(); _reflex?.Dispose(); }
    }
}
#endif
