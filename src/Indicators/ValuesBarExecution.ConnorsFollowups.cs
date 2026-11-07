#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedConnorsFollowupState : IMultiOutputState, IDisposable
    {
        private readonly PriceRsiWindow _priceRsi, _streakRsi;
        private ReturnOrderStatistic _rank;
        private readonly int _rankLength;
        private readonly RollingWindowMax? _high;
        private readonly RollingWindowMin? _low;
        private readonly StrengthAverage? _first, _second;
        private double _previous, _streak;
        private bool _started;
        public GeneratedConnorsFollowupState(int streakLength, int priceLength, int rankLength, int first, int second, int count, bool stochastic)
        {
            _priceRsi = new(MovingAvgType.WildersSmoothingMethod, priceLength, count);
            _streakRsi = new(MovingAvgType.WildersSmoothingMethod, streakLength, count);
            _rankLength = Math.Max(1, rankLength); _rank = new(_rankLength);
            if (stochastic)
            {
                var capacity = Math.Max(1, Math.Min(priceLength, count));
                _high = new(capacity); _low = new(capacity);
                _first = new(MovingAvgType.WildersSmoothingMethod, first, count);
                _second = new(MovingAvgType.WildersSmoothingMethod, second, count);
            }
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var price = bar.Close;
            var priceRsi = _priceRsi.Next(price, true);
            var rank = 100d * _rank.CountLessThan(price, _previous) / _rankLength;
            _rank.Add(price, _previous);
            var streak = !_started ? 0 : price > _previous ? _streak >= 0 ? _streak + 1 : 1
                : price < _previous ? _streak <= 0 ? _streak - 1 : -1 : 0;
            var streakRsi = _streakRsi.Next(streak, true);
            var connors = ConnorsValue.Combine(priceRsi, rank, streakRsi);
            if (_high is null)
            {
                outputs[0] = priceRsi; outputs[1] = rank; outputs[2] = streakRsi; outputs[3] = connors;
            }
            else
            {
                var high = _high.Add(connors, out _); var low = _low!.Add(connors, out _);
                var raw = ClampedRangePosition.Percent(connors, low, high);
                var line = _first!.Next(new StrengthValue(raw), true).Mantissa;
                outputs[0] = line; outputs[1] = _second!.Next(new StrengthValue(line), true).Mantissa;
            }
            _previous = price; _streak = streak; _started = true;
        }
        public void Reset()
        {
            _priceRsi.Reset(); _streakRsi.Reset(); _rank.Dispose(); _rank = new(_rankLength);
            _high?.Reset(); _low?.Reset(); _first?.Reset(); _second?.Reset(); _previous = _streak = 0; _started = false;
        }
        public void Dispose()
        {
            _priceRsi.Dispose(); _streakRsi.Dispose(); _rank.Dispose();
            _high?.Dispose(); _low?.Dispose(); _first?.Dispose(); _second?.Dispose();
        }
    }
}
#endif
