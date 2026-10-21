#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedStochasticComponentsState : IMultiOutputState, IDisposable
    {
        private readonly StochasticCustomWindow? _custom;
        private readonly Helpers.StochasticMomentumWindow? _momentum;
        public GeneratedStochasticComponentsState(int length, int firstLength, int secondLength, int signalLength, int count, bool momentum)
        {
            // An unreachable range expiry can be capped to the observed batch.
            // Smoothing periods keep their original weights and warmup rules.
            var capacity = Math.Max(1, Math.Min(length, count));
            if (momentum) _momentum = new(MovingAvgType.ExponentialMovingAverage, capacity, firstLength, secondLength, signalLength);
            else _custom = new(MovingAvgType.SimpleMovingAverage, capacity, firstLength, signalLength);
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var point = _custom is not null
                ? _custom.Next(bar.Close, bar.High, bar.Low, true)
                : _momentum!.Next(bar.Close, bar.High, bar.Low, true);
            outputs[0] = point.Line; outputs[1] = point.Signal;
        }
        public void Reset() { _custom?.Reset(); _momentum?.Reset(); }
        public void Dispose() { _custom?.Dispose(); _momentum?.Dispose(); }
    }
}
#endif
