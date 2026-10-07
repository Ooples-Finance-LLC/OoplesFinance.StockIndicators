using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Deviation-scaled average using a two-pole filter of price minus the previous average.</summary>
/// <remarks>The first close seeds the average. Filtered deviations use a fixed-period RMS,
/// including incomplete windows. The normalized magnitude is the correctly rounded square
/// root of period*filter^2/sum(filter^2), with zero for a zero denominator. Gain is clamped
/// to [0.1,1]. Differences, filter stages, and convex recurrences round once with wide
/// intermediates. Filter history grows lazily and resets completely.</remarks>
public sealed class FilteredDeviationAverage
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period average with finite scale in (0,1].</summary>
    public FilteredDeviationAverage(int period = 14, double scaleFactor = .9)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(scaleFactor) || scaleFactor <= 0 || scaleFactor > 1)
            throw new ArgumentOutOfRangeException(nameof(scaleFactor));
        Period = period;
        ScaleFactor = scaleFactor;
    }

    /// <summary>Filter and RMS window period.</summary>
    public int Period { get; }

    /// <summary>Adaptive gain scale.</summary>
    public double ScaleFactor { get; }

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, (long)Period * 3 / 2);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, ScaleFactor);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars => RangeAdaptiveReference.Deviation(bars, Period, ScaleFactor),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State : IIndicatorState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private readonly int _period;
        private readonly double _scale;
        private readonly BigInteger _c1,
            _c2,
            _c3;
        private readonly Queue<BigInteger> _squares = new();
        private BigInteger _average,
            _zero,
            _filter,
            _olderFilter,
            _sum;
        private bool _seeded;

        internal State(int period, double scale)
        {
            _period = period;
            _scale = scale;
            var a = Math.Exp(-1.414 * Math.PI / (.5 * period));
            var b = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
            var c = -a * a;
            _c1 = ExactVarianceWindow.Units(1 - b - c);
            _c2 = ExactVarianceWindow.Units(b);
            _c3 = ExactVarianceWindow.Units(c);
        }

        public void Reset()
        {
            _squares.Clear();
            _average = _zero = _filter = _olderFilter = _sum = 0;
            _seeded = false;
        }

        public double Update(in Bar bar)
        {
            var price = ExactVarianceWindow.Units(bar.Close);
            if (!_seeded)
            {
                _seeded = true;
                _average = price;
                return bar.Close;
            }
            var zero = RocBankValue.RoundUnits(price - _average, 1);
            var filter = RocBankValue.RoundUnits(
                _c1 * (zero + _zero) + 2 * _c2 * _filter + 2 * _c3 * _olderFilter,
                2 * Grid
            );
            var square = filter * filter;
            if (_squares.Count == _period)
                _sum -= _squares.Dequeue();
            _squares.Enqueue(square);
            _sum += square;
            var normalized = _sum.IsZero
                ? 0
                : ExactPopulationDeviation.RootRatio((square * _period) << 2148, _sum);
            var gain = Math.Max(.1, Math.Min(1, _scale * normalized * 5 / _period));
            var alpha = ExactVarianceWindow.Units(gain);
            _average = RocBankValue.RoundUnits(alpha * price + (Grid - alpha) * _average, Grid);
            _zero = zero;
            _olderFilter = _filter;
            _filter = filter;
            return ExactMeanAccumulator.UnitRatio(_average, 1);
        }
    }
}
