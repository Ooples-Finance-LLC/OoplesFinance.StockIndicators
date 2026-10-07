using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Initialization and zero-denominator convention for triple exponential rate.</summary>
public enum TripleRateSeed
{
    /// <summary>All three stages start from one price mean; zero divided by zero is absent.</summary>
    SharedMean,

    /// <summary>Each stage starts from its own mean; zero denominator returns zero.</summary>
    CascadedMeans,

    /// <summary>Each stage starts from its first available input; zero denominator returns zero.</summary>
    CascadedFirstPrice,
}

/// <summary>TRIX percent change, third EMA and an optional full-window SMA signal.</summary>
/// <remarks>Each EMA stage rounds once using exact weight 2/(period+1). The
/// percentage change rounds once after exact subtraction and division. SharedMean
/// publishes from index period. Cascaded modes publish EMA3 at three times the
/// stage lookback and rate one bar later; stage lookback is period-1+suppression.
/// Suppression applies separately to each stage and is unavailable in SharedMean.
/// A shared zero denominator gives absence for zero/zero and rejects unbounded
/// change as output overflow. Cascaded zero denominators return zero. Signals
/// require a complete window of present rates. State and histories grow lazily.</remarks>
public sealed class TripleExponentialRate : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates triple EMA rate with positive periods and per-stage startup suppression.</summary>
    public TripleExponentialRate(
        int period = 14,
        TripleRateSeed seed = TripleRateSeed.SharedMean,
        int? signalPeriod = null,
        int suppression = 0
    )
        : base(6)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (seed < TripleRateSeed.SharedMean || seed > TripleRateSeed.CascadedFirstPrice)
            throw new ArgumentOutOfRangeException(nameof(seed));
        if (signalPeriod is <= 0)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        if (suppression < 0 || seed == TripleRateSeed.SharedMean && suppression != 0)
            throw new ArgumentOutOfRangeException(nameof(suppression));
        Period = period;
        Seed = seed;
        SignalPeriod = signalPeriod;
        Suppression = suppression;
    }

    /// <summary>EMA period.</summary>
    public int Period { get; }

    /// <summary>Startup and denominator convention.</summary>
    public TripleRateSeed Seed { get; }

    /// <summary>Optional signal window.</summary>
    public int? SignalPeriod { get; }

    /// <summary>Extra startup bars per cascaded stage.</summary>
    public int Suppression { get; }

    /// <summary>Percent change or zero when absent.</summary>
    public IIndicatorOutput Rate => Outputs[0];

    /// <summary>Third EMA or zero when absent.</summary>
    public IIndicatorOutput Ema3 => Outputs[1];

    /// <summary>Signal mean or zero when absent.</summary>
    public IIndicatorOutput Signal => Outputs[2];

    /// <summary>Rate presence.</summary>
    public IIndicatorOutput RateIsDefined => Outputs[3];

    /// <summary>EMA3 presence.</summary>
    public IIndicatorOutput Ema3IsDefined => Outputs[4];

    /// <summary>Signal presence.</summary>
    public IIndicatorOutput SignalIsDefined => Outputs[5];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Seed, SignalPeriod, Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        TripleRateReference
                            .Calculate(bars, Period, Seed, SignalPeriod, Suppression)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, TripleRateSeed seed, int? signalPeriod, int suppression)
        : IMultiOutputState
    {
        private readonly BigInteger[] _values = new BigInteger[3],
            _sums = new BigInteger[3];
        private readonly Queue<double?> _signal = new();
        private BigInteger _signalSum,
            _previousThird;
        private int _missing;
        private long _index = -1;
        private bool _hasThird;

        public void Reset()
        {
            Array.Clear(_values, 0, 3);
            Array.Clear(_sums, 0, 3);
            _signal.Clear();
            _signalSum = _previousThird = 0;
            _missing = 0;
            _index = -1;
            _hasThird = false;
        }

        private BigInteger Smooth(BigInteger prior, BigInteger input) =>
            RocBankValue.RoundUnits(prior * (period - 1) + 2 * input, (long)period + 1);

        private static void Publish(double? value, int slot, Span<double> output)
        {
            if (value.HasValue)
            {
                output[slot] = value.Value;
                output[slot + 3] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var input = ExactVarianceWindow.Units(bar.Close);
            if (seed == TripleRateSeed.SharedMean && _index < period)
            {
                _sums[0] += input;
                if (_index == period - 1)
                {
                    var initial = RocBankValue.RoundUnits(_sums[0], period);
                    for (var j = 0; j < 3; j++)
                        _values[j] = initial;
                    _previousThird = initial;
                    _hasThird = true;
                    _sums[0] = 0;
                }
                return;
            }
            for (var j = 0; j < 3; j++)
            {
                if (seed == TripleRateSeed.SharedMean)
                    _values[j] = Smooth(_values[j], input);
                else
                {
                    var lookback = (long)period - 1 + suppression;
                    var start = j * lookback;
                    if (_index < start)
                        return;
                    if (seed == TripleRateSeed.CascadedFirstPrice)
                        _values[j] = _index == start ? input : Smooth(_values[j], input);
                    else if (_index < start + period)
                    {
                        _sums[j] += input;
                        if (_index != start + period - 1)
                            return;
                        _values[j] = RocBankValue.RoundUnits(_sums[j], period);
                        _sums[j] = 0;
                    }
                    else
                        _values[j] = Smooth(_values[j], input);
                    if (_index < start + lookback)
                        return;
                }
                input = _values[j];
            }
            Publish(ExactMeanAccumulator.UnitRatio(input, 1), 1, output);
            if (!_hasThird)
            {
                _hasThird = true;
                _previousThird = input;
                return;
            }
            double? rate;
            if (_previousThird.IsZero)
                rate =
                    seed != TripleRateSeed.SharedMean ? 0
                    : input.IsZero ? null
                    : input.Sign > 0 ? double.PositiveInfinity
                    : double.NegativeInfinity;
            else
                rate = ExactMeanAccumulator.UnitRatio(
                    (100 * (input - _previousThird) * _previousThird.Sign) << 1074,
                    BigInteger.Abs(_previousThird)
                );
            _previousThird = input;
            Publish(rate, 0, output);
            if (
                !signalPeriod.HasValue
                || rate.HasValue && (double.IsInfinity(rate.Value) || double.IsNaN(rate.Value))
            )
                return;
            if (_signal.Count == signalPeriod.Value)
            {
                var old = _signal.Dequeue();
                if (old.HasValue)
                    _signalSum -= ExactVarianceWindow.Units(old.Value);
                else
                    _missing--;
            }
            _signal.Enqueue(rate);
            if (rate.HasValue)
                _signalSum += ExactVarianceWindow.Units(rate.Value);
            else
                _missing++;
            if (_signal.Count == signalPeriod.Value && _missing == 0)
                Publish(ExactMeanAccumulator.UnitRatio(_signalSum, signalPeriod.Value), 2, output);
        }
    }
}
