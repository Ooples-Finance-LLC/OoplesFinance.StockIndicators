#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class DeviationRatioValueState(DeviationRatioAdaptiveAverage owner, int count) : IMultiOutputState
    {
        private readonly DeviationRatioMoments _short = new(owner.ShortPeriod, count),
            _long = new(owner.LongPeriod, count);
        private JurikDyadic _average;
        private long _count;
        private bool _undefined;

        public void Reset()
        {
            _short.Reset(); _long.Reset();
            _average = default; _count = 0; _undefined = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _short.Add(bar.Close); _long.Add(bar.Close); _count++;
            if (_undefined) return;
            if (_count <= owner.LongPeriod)
                _average = JurikDyadic.FromDouble(_short.Mean());
            else
            {
                var longSpread = _long.Spread;
                if (longSpread.IsZero) { _undefined = true; return; }
                var numerator = _short.Spread * _long.Count * _long.Count;
                var denominator = longSpread * _short.Count * _short.Count;
                int power = 2 * (_short.Grid - _long.Grid), shift = 0;
                double ratio = ExactPopulationDeviation.ScaledRootRatio(numerator, denominator, power);
                // Match the original extended-exponent rounding before applying
                // alpha, including zero alpha when the ratio exceeds binary64.
                while (double.IsInfinity(ratio))
                {
                    shift += 512;
                    ratio = ExactPopulationDeviation.ScaledRootRatio(numerator, denominator, power - 2 * shift);
                }
                var gain = JurikDyadic.FromDouble(ratio);
                gain.ScaleByPowerOfTwo(shift); gain.Multiply(owner.Alpha); gain = gain.Rounded();
                var difference = JurikDyadic.FromDouble(bar.Close);
                difference.Subtract(_average); difference.Multiply(gain); difference.AddExact(_average);
                _average = difference.Rounded();
            }
            output[0] = _average.Mean(1); output[1] = 1;
        }
    }

    // Keep exact moments on the smallest grid encountered, rather than padding
    // ordinary prices to 2^-1074. Expiration uses the same grid as insertion.
    private sealed class DeviationRatioMoments(int period, int count)
    {
        private readonly Queue<double> _values = new(Math.Min(period, count));
        private BigInteger _sum, _squares;
        internal int Grid { get; private set; }
        internal int Count => _values.Count;
        internal BigInteger Spread => Count * _squares - _sum * _sum;
        internal double Mean() => ExactMeanAccumulator.ScaledRatio(_sum, Count, Grid);

        internal void Reset() { _values.Clear(); _sum = _squares = 0; Grid = 0; }

        internal void Add(double value)
        {
            if (Count == period)
            {
                var (old, exponent) = ExactMeanAccumulator.DecomposeFinite(_values.Dequeue());
                var expired = old == 0 ? BigInteger.Zero : new BigInteger(old) << (exponent - Grid);
                _sum -= expired; _squares -= expired * expired;
            }
            var (integer, grid) = ExactMeanAccumulator.DecomposeFinite(value);
            if (Count == 0) Grid = grid;
            if (integer != 0 && grid < Grid)
            {
                int shift = Grid - grid;
                _sum <<= shift; _squares <<= 2 * shift; Grid = grid;
            }
            var current = integer == 0 ? BigInteger.Zero : new BigInteger(integer) << (grid - Grid);
            _sum += current; _squares += current * current; _values.Enqueue(value);
        }
    }
}
#endif
