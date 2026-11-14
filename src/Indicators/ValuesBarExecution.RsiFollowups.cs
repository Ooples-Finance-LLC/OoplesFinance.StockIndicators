#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedRsiFollowupState : IMultiOutputState, IDisposable
    {
        private readonly RapidGainLossWindow? _rapid;
        private readonly PriceRsiWindow? _rsi;
        private readonly RollingWindowMax? _high;
        private readonly RollingWindowMin? _low;
        private readonly StrengthAverage _first;
        private readonly StrengthAverage? _second;
        public GeneratedRsiFollowupState(int length, int stochLength, int firstLength, int signalLength, int count, bool rapid)
        {
            if (rapid)
            {
                _rapid = new(length, count);
                _first = new(MovingAvgType.ExponentialMovingAverage, length, count);
            }
            else
            {
                _rsi = new(MovingAvgType.WildersSmoothingMethod, length, count);
                var capacity = Math.Max(1, Math.Min(stochLength, count));
                _high = new(capacity); _low = new(capacity);
                _first = new(MovingAvgType.WildersSmoothingMethod, firstLength, count);
                _second = new(MovingAvgType.WildersSmoothingMethod, signalLength, count);
            }
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_rapid is not null)
            {
                var value = _rapid.Next(bar.Close, true);
                outputs[0] = value; outputs[1] = _first.Next(new StrengthValue(value), true).Mantissa;
            }
            else
            {
                var value = _rsi!.Next(bar.Close, true);
                var high = _high!.Add(value, out _); var low = _low!.Add(value, out _);
                var raw = ClampedRangePosition.Percent(value, low, high);
                var line = _first.Next(new StrengthValue(raw), true).Mantissa;
                outputs[0] = line; outputs[1] = _second!.Next(new StrengthValue(line), true).Mantissa;
            }
        }
        public void Reset() { _rapid?.Reset(); _rsi?.Reset(); _high?.Reset(); _low?.Reset(); _first.Reset(); _second?.Reset(); }
        public void Dispose() { _rapid?.Dispose(); _rsi?.Dispose(); _high?.Dispose(); _low?.Dispose(); _first.Dispose(); _second?.Dispose(); }
    }
}
#endif
