#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedTurboFollowupState : IMultiOutputState, IDisposable
    {
        private readonly TurboTriggerWindow? _trigger;
        private readonly TurboStochasticsWindow? _stochastic;
        public GeneratedTurboFollowupState(int length, int fit, int turbo, int count, int kind)
        {
            if (kind == 0) _trigger = new(MovingAvgType.SimpleMovingAverage, length, 2, count);
            else _stochastic = new(MovingAvgType.SimpleMovingAverage, length, fit, turbo, kind == 2);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_trigger is not null)
            {
                var point = _trigger.Next(bar.Close, bar.Open, bar.High, bar.Low, true);
                outputs[0] = point.Bull; outputs[1] = point.Trigger;
            }
            else
            {
                var point = _stochastic!.Next(bar.Close, bar.High, bar.Low, true);
                outputs[0] = point.Line; outputs[1] = point.SignalLine;
            }
        }
        public void Reset() { _trigger?.Reset(); _stochastic?.Reset(); }
        public void Dispose() { _trigger?.Dispose(); _stochastic?.Dispose(); }
    }
}
#endif
