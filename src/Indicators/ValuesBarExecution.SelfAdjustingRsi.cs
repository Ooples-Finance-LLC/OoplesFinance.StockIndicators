#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedSelfAdjustingRsiState : IMultiOutputState, IDisposable
    {
        private readonly PriceRsiWindow _rsi;
        private readonly StrengthAverage _signal;
        private readonly ExactPopulationWindow? _population;
        private readonly double _multiplier;
        public GeneratedSelfAdjustingRsiState(int length, int smoothing, double multiplier, int count)
        {
            _rsi = new(MovingAvgType.SimpleMovingAverage, length, count);
            _signal = new(MovingAvgType.SimpleMovingAverage, smoothing, count);
            // Full-period deviation is zero throughout a shorter batch.
            if (Math.Max(1, length) <= count) _population = new(length);
            _multiplier = multiplier;
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var line = _rsi.Next(bar.Close, true);
            var signal = _signal.Next(new StrengthValue(line), true).Mantissa;
            var adjustment = _multiplier * (_population?.Next(line, true) ?? 0);
            outputs[0] = line; outputs[1] = signal; outputs[2] = 50 + adjustment; outputs[3] = 50 - adjustment;
        }
        public void Reset() { _rsi.Reset(); _signal.Reset(); _population?.Reset(); }
        public void Dispose() { _rsi.Dispose(); _signal.Dispose(); _population?.Dispose(); }
    }
}
#endif
