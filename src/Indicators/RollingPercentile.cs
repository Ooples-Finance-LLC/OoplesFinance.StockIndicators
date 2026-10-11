using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Linearly interpolated percentile of a rolling close window; 0.5 is the median.</summary>
/// <remarks>The rank is percentile*(count-1). Rank and interpolation use the exact supplied
/// binary64 percentile, with one final rounding. Startup uses available history, or its
/// arithmetic mean when averageDuringWarmup is enabled. Storage grows with received history.</remarks>
public sealed class RollingPercentile : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a percentile with a positive period and a finite fraction in [0,1].</summary>
    public RollingPercentile(
        int period = 20,
        double percentile = 0.5,
        bool averageDuringWarmup = false
    )
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(percentile) || percentile < 0 || percentile > 1)
            throw new ArgumentOutOfRangeException(nameof(percentile));
        Period = period;
        Percentile = percentile;
        AverageDuringWarmup = averageDuringWarmup;
    }

    /// <summary>Maximum number of recent closes.</summary>
    public int Period { get; }

    /// <summary>Fractional rank, from zero (minimum) to one (maximum).</summary>
    public double Percentile { get; }

    /// <summary>Whether incomplete windows publish their mean instead of their percentile.</summary>
    public bool AverageDuringWarmup { get; }

#if !NETFRAMEWORK
    // A fractional full-window rank always uses arithmetic interpolation, which
    // ignores equal-zero ordering. Integer ranks expose the sorted list's zero bits.
    internal bool HasFractionalFullRank
    {
        get
        {
            ulong bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(Percentile));
            int exponent = (int)((bits >> 52) & 0x7ff);
            ulong significand = bits & 0x000fffffffffffffUL;
            if (exponent != 0) significand |= 1UL << 52;
            return Period > 1 && significand != 0
                && BitOperations.TrailingZeroCount(significand) + BitOperations.TrailingZeroCount((uint)(Period - 1))
                    < (exponent == 0 ? 1074 : 1075 - exponent);
        }
    }
#endif

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Percentile, AverageDuringWarmup);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [IndicatorValidationRule.Reference(0, Reference, 0, 0)];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        var fraction = ReferenceFraction.FromDouble(Percentile);
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(Period, i + 1);
            var sorted = bars.Skip(i + 1 - count)
                .Take(count)
                .Select(b => b.Close)
                .OrderBy(value => value)
                .ToArray();
            if (AverageDuringWarmup && count < Period)
                values[i] = (
                    sorted.Aggregate(
                        new ReferenceFraction(0),
                        (sum, v) => sum + ReferenceFraction.FromDouble(v)
                    ) / new ReferenceFraction(count)
                ).ToDouble();
            else
            {
                var position = fraction * new ReferenceFraction(count - 1);
                var (top, bottom) = position.Components;
                var index = (int)(top / bottom);
                var weight = position - new ReferenceFraction(index);
                var low = ReferenceFraction.FromDouble(sorted[index]);
                var high = ReferenceFraction.FromDouble(sorted[Math.Min(index + 1, count - 1)]);
                values[i] = (low + (high - low) * weight).ToDouble();
            }
        }
        return values;
    }

    internal sealed class State : IIndicatorState
    {
        private readonly int _period;
        private readonly bool _average;
        private readonly BigInteger _top,
            _bottom;
        private readonly Queue<double> _history = new();
        private readonly List<double> _sorted = new();
        private ExactMeanAccumulator _sum;
        private bool _warming;
        private readonly int _fullIndex;
        private readonly BigInteger _fullRemainder, _fullLowerWeight;
        private readonly ExactMeanAccumulator _denominator;

        internal State(int period, double percentile, bool average)
        {
            _period = period;
            _average = average;
            _warming = average;
            // The normalized binary significand is already coprime to its
            // power-of-two denominator; avoid minimum-unit padding and a GCD
            // for every independently seeded worker.
            var factor = new ExactMeanAccumulator.ProductFactor(percentile);
            _top = factor.Mantissa;
            _bottom = factor.Mantissa == 0 ? BigInteger.One
                : BigInteger.One << (1074 - factor.Scale);
            _fullIndex = (int)BigInteger.DivRem(_top * (period - 1), _bottom, out _fullRemainder);
            _fullLowerWeight = _bottom - _fullRemainder;
            var denominator = new ExactMeanAccumulator();
            denominator.Add(1, _bottom);
            _denominator = denominator;
        }

        public void Reset()
        {
            _history.Clear();
            _sorted.Clear();
            _sum = default;
            _warming = _average;
        }

        public double Update(in Bar bar)
        {
            if (_history.Count == _period)
            {
                var oldest = _history.Dequeue();
                _sorted.RemoveAt(_sorted.BinarySearch(oldest));
            }
            var insertion = _sorted.BinarySearch(bar.Close);
            _sorted.Insert(insertion < 0 ? ~insertion : insertion, bar.Close);
            _history.Enqueue(bar.Close);
            if (_warming)
            {
                if (_history.Count < _period)
                {
                    _sum.Add(bar.Close);
                    return _sum.Mean(_history.Count);
                }
                // A complete window never returns to warmup until Reset.
                _sum = default;
                _warming = false;
            }
            bool full = _history.Count == _period;
            BigInteger remainder;
            int index;
            if (full) { index = _fullIndex; remainder = _fullRemainder; }
            else index = (int)BigInteger.DivRem(_top * (_history.Count - 1), _bottom, out remainder);
            if (remainder.IsZero)
                return _sorted[index];
            if (_bottom == 2)
            {
                double total = _sorted[index] + _sorted[index + 1];
                if (total == 0) return 0; // NOSONAR: S1244 - Exact cancellation alone produces canonical zero.
                if (FrameworkCompatibility.IsFinite(total) && Math.Abs(total) >= 4.450147717014403E-308)
                    return total * .5;
                var mean = new ExactMeanAccumulator();
                mean.Add(_sorted[index]); mean.Add(_sorted[index + 1]);
                return mean.Mean(2);
            }
            var numerator = new ExactMeanAccumulator();
            numerator.Add(_sorted[index], full ? _fullLowerWeight : _bottom - remainder);
            numerator.Add(_sorted[index + 1], remainder);
            return numerator.Ratio(_denominator);
        }
    }
}
