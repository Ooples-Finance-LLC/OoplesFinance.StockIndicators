#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class FractalValueState(int period, int count) : IIndicatorState
    {
        private readonly double[] _window = new double[Math.Min(period, Math.Max(1, count))];
        private JurikDyadic _sum, _average;
        private int _position, _seen;

        public void Reset()
        {
            _position = _seen = 0;
            _sum = _average = default;
        }

        public double Update(in Bar bar)
        {
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            if (_seen < period) _seen++;
            if (_seen < period)
            {
                _sum.Add(bar.Close);
                _average = _sum.Rounded(_seen);
            }
            else
            {
                // The ring's next write position is the oldest observation.
                // Preserve the floor(P/2) split, including odd periods.
                int half = period / 2;
                double firstHigh = _window[_position], firstLow = firstHigh;
                int position = _position;
                for (int i = 1; i < half; i++)
                {
                    if (++position == period) position = 0;
                    firstHigh = Math.Max(firstHigh, _window[position]);
                    firstLow = Math.Min(firstLow, _window[position]);
                }
                if (++position == period) position = 0;
                double secondHigh = _window[position], secondLow = secondHigh;
                for (int i = half + 1; i < period; i++)
                {
                    if (++position == period) position = 0;
                    secondHigh = Math.Max(secondHigh, _window[position]);
                    secondLow = Math.Min(secondLow, _window[position]);
                }
                var whole = JurikDyadic.FromDouble(Math.Max(firstHigh, secondHigh));
                whole.Subtract(JurikDyadic.FromDouble(Math.Min(firstLow, secondLow)));
                var split = JurikDyadic.FromDouble(firstHigh);
                split.Subtract(JurikDyadic.FromDouble(firstLow));
                split.Add(secondHigh); split.Subtract(JurikDyadic.FromDouble(secondLow));
                // Epsilon is one2^-1074 unit. These integer multiples are
                // exactly representable, even for int.MaxValue periods. Keep
                // the epsilon through the exact ratio, including wide ranges.
                if (!TryFractalRatio(in split, in whole, period, out double ratio))
                {
                    split.Add(half * double.Epsilon); split.Multiply(period);
                    whole.Add(period * double.Epsilon); whole.Multiply(half);
                    ratio = split.Ratio(whole);
                }
                double dimension = Math.Log(ratio) / Math.Log(2);
                double gain = Math.Max(.01, Math.Min(1, Math.Exp(-4.6 * (dimension - 1))));
                var price = JurikDyadic.FromDouble(bar.Close);
                if (JurikDyadic.TryBlend(_average, price, gain, out var blended)) _average = blended;
                else
                {
                    price.Subtract(_average); price.Multiply(gain); price.AddExact(_average);
                    _average = price.Rounded();
                }
            }
            return _average.Mean(1);
        }
    }

    private static bool TryFractalRatio(in JurikDyadic split, in JurikDyadic whole, int period, out double ratio)
    {
        ratio = 0;
        if (period > 4096 || !split.TryDouble(out double s) || !whole.TryDouble(out double w)) return false;
        if (w == 0) { ratio = 1; return true; }
        if (s == 0) return false;
        int grid = Math.Min(ExactMeanAccumulator.DecomposeFinite(s).Exponent,
            ExactMeanAccumulator.DecomposeFinite(w).Exponent);
        if (grid < -800) return false;
        double numerator = Math.ScaleB(s, -grid) * period;
        double denominator = Math.ScaleB(w, -grid) * (period / 2);
        const double limit = 1125899906842624d; //2^50; all integer products are exact.
        if (numerator > limit || denominator > limit) return false;
        // The true ratio is (N*X+C)/(D*X+C), X=2^(grid+1074)>=2^274,
        // C=P*floor(P/2)<2^24, and 1<=N,D<=2^50. Its distance from N/D
        // is <2^-200. N/D cannot be a binary64 midpoint: its reduced numerator
        // has <=51 bits, whereas a normal midpoint has an odd54-bit numerator.
        // Since 2^-50<=N/D<=2^50, its distance to any rounding midpoint is
        // >=1/(D*2^104)>=2^-154. Thus epsilon cannot alter either rounding
        // decision. Outside this certificate retain the complete exact ratio.
        ratio = numerator / denominator;
        return true;
    }
}
#endif
