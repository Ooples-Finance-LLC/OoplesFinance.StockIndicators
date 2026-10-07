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
        if (!double.IsFinite(percentile) || percentile < 0 || percentile > 1)
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
                .Order()
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

    private sealed class State : IIndicatorState
    {
        private readonly int _period;
        private readonly bool _average;
        private readonly BigInteger _top,
            _bottom;
        private readonly Queue<double> _history = new();
        private readonly List<double> _sorted = new();
        private ExactMeanAccumulator _sum;

        internal State(int period, double percentile, bool average)
        {
            _period = period;
            _average = average;
            var top = ExactVarianceWindow.Units(percentile);
            var bottom = BigInteger.One << 1074;
            var gcd = BigInteger.GreatestCommonDivisor(top, bottom);
            _top = top / gcd;
            _bottom = bottom / gcd;
        }

        public void Reset()
        {
            _history.Clear();
            _sorted.Clear();
            _sum = default;
        }

        public double Update(in Bar bar)
        {
            if (_history.Count == _period)
            {
                var oldest = _history.Dequeue();
                _sorted.RemoveAt(_sorted.BinarySearch(oldest));
                _sum.Add(oldest, -1);
            }
            var insertion = _sorted.BinarySearch(bar.Close);
            _sorted.Insert(insertion < 0 ? ~insertion : insertion, bar.Close);
            _history.Enqueue(bar.Close);
            _sum.Add(bar.Close);
            if (_average && _history.Count < _period)
                return _sum.Mean(_history.Count);
            var index = (int)
                BigInteger.DivRem(_top * (_history.Count - 1), _bottom, out var remainder);
            if (remainder.IsZero)
                return _sorted[index];
            var numerator = new ExactMeanAccumulator();
            numerator.Add(_sorted[index], _bottom - remainder);
            numerator.Add(_sorted[index + 1], remainder);
            var denominator = new ExactMeanAccumulator();
            denominator.Add(1, _bottom);
            return numerator.Ratio(denominator);
        }
    }
}
