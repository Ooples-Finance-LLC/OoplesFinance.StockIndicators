#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedQuasiNoiseState : IMultiOutputState, IDisposable
    {
        private readonly PriceRsiWindow _priceRsi, _streakRsi;
        private readonly StrengthAverage _average;
        private readonly ExactPopulationWindow? _deviation;
        private readonly int _rankLength;
        private readonly double _divisor;
        private ReturnOrderStatistic _rank;
        private double _previous, _streak;
        private bool _started;
        public GeneratedQuasiNoiseState(int length, int noiseLength, double divisor, int count)
        {
            _rankLength = Math.Max(1, length); _rank = new(_rankLength); _divisor = divisor;
            _priceRsi = new(MovingAvgType.WildersSmoothingMethod, noiseLength, count);
            _streakRsi = new(MovingAvgType.WildersSmoothingMethod, noiseLength, count);
            _average = new(MovingAvgType.WildersSmoothingMethod, noiseLength, count);
            // Population deviation remains zero before the entire window is full.
            if (Math.Max(1, noiseLength) <= count) _deviation = new(noiseLength);
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
            var noise = (connors - 50) * (1 / _divisor);
            var deviation = _deviation?.Next(noise, true) ?? 0;
            outputs[0] = noise; outputs[1] = _average.Next(new StrengthValue(noise), true).Mantissa;
            outputs[2] = deviation; outputs[3] = Pow(deviation, 2);
            _previous = price; _streak = streak; _started = true;
        }
        public void Reset()
        {
            _priceRsi.Reset(); _streakRsi.Reset(); _average.Reset(); _deviation?.Reset();
            _rank.Dispose(); _rank = new(_rankLength); _previous = _streak = 0; _started = false;
        }
        public void Dispose()
        {
            _priceRsi.Dispose(); _streakRsi.Dispose(); _average.Dispose(); _deviation?.Dispose(); _rank.Dispose();
        }
    }
}
#endif
