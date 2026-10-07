using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Adaptive average weighted by the ratio of short and long population deviations.</summary>
/// <remarks>Publishes the available short-window mean for the first LongPeriod bars.
/// Thereafter the gain is alpha times sqrt(short variance / long variance).
/// The ratio, gain and complete recurrence each round once with extended upper exponents. Exact moments prevent
/// overflow and cancellation. A zero long variance makes the recurrence undefined
/// until reset, represented by zero and a false presence output. History grows lazily.</remarks>
public sealed class DeviationRatioAdaptiveAverage
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates positive windows and a finite gain; zero longPeriod selects four times shortPeriod.</summary>
    public DeviationRatioAdaptiveAverage(
        int shortPeriod = 20,
        int longPeriod = 0,
        double alpha = .2
    )
        : base(2)
    {
        if (shortPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(shortPeriod));
        var length = longPeriod == 0 ? 4L * shortPeriod : longPeriod;
        if (length < 1 || length > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(longPeriod));
        if (!double.IsFinite(alpha))
            throw new ArgumentOutOfRangeException(nameof(alpha));
        ShortPeriod = shortPeriod;
        LongPeriod = (int)length;
        Alpha = alpha;
    }

    /// <summary>Short deviation and startup mean window.</summary>
    public int ShortPeriod { get; }

    /// <summary>Long deviation window and startup duration.</summary>
    public int LongPeriod { get; }

    /// <summary>Finite signed multiplier of the deviation ratio.</summary>
    public double Alpha { get; }

    /// <summary>Adaptive value, or zero if undefined.</summary>
    public IIndicatorOutput Average => Outputs[0];

    /// <summary>One while the recurrence is defined, otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override int WarmupBars => LongPeriod;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        DeviationRatioAdaptiveReference
                            .Values(bars, this)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class Moments(int period)
    {
        private readonly Queue<BigInteger> _values = new();
        private BigInteger _sum,
            _squares;
        internal int Count => _values.Count;
        internal BigInteger Sum => _sum;
        internal BigInteger Spread => Count * _squares - _sum * _sum;

        internal void Reset()
        {
            _values.Clear();
            _sum = _squares = 0;
        }

        internal void Add(BigInteger value)
        {
            if (Count == period)
            {
                var old = _values.Dequeue();
                _sum -= old;
                _squares -= old * old;
            }
            _values.Enqueue(value);
            _sum += value;
            _squares += value * value;
        }
    }

    private sealed class State(DeviationRatioAdaptiveAverage owner) : IMultiOutputState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private readonly Moments _short = new(owner.ShortPeriod),
            _long = new(owner.LongPeriod);
        private readonly BigInteger _alpha = ExactVarianceWindow.Units(owner.Alpha);
        private BigInteger _average;
        private long _count;
        private bool _undefined;

        public void Reset()
        {
            _short.Reset();
            _long.Reset();
            _average = 0;
            _count = 0;
            _undefined = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var price = ExactVarianceWindow.Units(bar.Close);
            _short.Add(price);
            _long.Add(price);
            _count++;
            if (_undefined)
                return;
            if (_count <= owner.LongPeriod)
                _average = RocBankValue.RoundUnits(_short.Sum, _short.Count);
            else
            {
                if (_long.Spread.IsZero)
                {
                    _undefined = true;
                    return;
                }
                var numerator = (_short.Spread * _long.Count * _long.Count) << 2148;
                var denominator = _long.Spread * _short.Count * _short.Count;
                var shift = 0;
                var ratio = ExactPopulationDeviation.RootRatio(numerator, denominator);
                while (double.IsInfinity(ratio))
                {
                    denominator <<= 1024;
                    shift += 512;
                    ratio = ExactPopulationDeviation.RootRatio(numerator, denominator);
                }
                var ratioUnits = ExactVarianceWindow.Units(ratio) << shift;
                var gain = RocBankValue.RoundUnits(_alpha * ratioUnits, Grid);
                _average = RocBankValue.RoundUnits(gain * price + (Grid - gain) * _average, Grid);
            }
            output[0] = ExactMeanAccumulator.UnitRatio(_average, 1);
            output[1] = 1;
        }
    }
}
