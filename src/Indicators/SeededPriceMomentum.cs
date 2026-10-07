using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Mean-seeded price momentum with 2/period smoothing and an EMA signal.</summary>
/// <remarks>One-bar percent changes feed a mean-seeded 2/period recurrence.
/// Its rounded result is multiplied by ten, then smoothed with 2/smoothPeriod.
/// A separately mean-seeded 2/(signalPeriod+1) recurrence supplies the signal.
/// Every arithmetic stage rounds once with extended upper exponents. Zero prior
/// price makes the return absent and permanently poisons the later recurrences.
/// Only final published overflow is rejected. State is constant in the periods.</remarks>
public sealed class SeededPriceMomentum : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates PMO with first period at least two and positive smoothing/signal periods.</summary>
    public SeededPriceMomentum(int period = 35, int smoothPeriod = 20, int signalPeriod = 10)
        : base(4)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (smoothPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(smoothPeriod));
        if (signalPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        Period = period;
        SmoothPeriod = smoothPeriod;
        SignalPeriod = signalPeriod;
    }

    /// <summary>Initial return mean and smoothing period.</summary>
    public int Period { get; }

    /// <summary>Second smoothing period.</summary>
    public int SmoothPeriod { get; }

    /// <summary>Signal EMA period.</summary>
    public int SignalPeriod { get; }

    /// <summary>Price momentum or zero when absent.</summary>
    public IIndicatorOutput Momentum => Outputs[0];

    /// <summary>Signal or zero when absent.</summary>
    public IIndicatorOutput Signal => Outputs[1];

    /// <summary>Momentum presence.</summary>
    public IIndicatorOutput MomentumIsDefined => Outputs[2];

    /// <summary>Signal presence.</summary>
    public IIndicatorOutput SignalIsDefined => Outputs[3];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, SmoothPeriod, SignalPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        SeededMomentumReference
                            .Pmo(bars, Period, SmoothPeriod, SignalPeriod)[slot % 2]
                            .Select(v =>
                                slot < 2 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, int smooth, int signal) : IMultiOutputState
    {
        private long _index = -1;
        private BigInteger _previous,
            _first,
            _second,
            _signal,
            _firstSum,
            _secondSum,
            _signalSum;
        private bool _missing;

        private static BigInteger R(BigInteger x, BigInteger n) => RocBankValue.RoundUnits(x, n);

        public void Reset()
        {
            _index = -1;
            _previous = _first = _second = _signal = _firstSum = _secondSum = _signalSum = 0;
            _missing = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var value = ExactVarianceWindow.Units(bar.Close);
            if (++_index == 0)
            {
                _previous = value;
                return;
            }
            if (_previous.IsZero)
                _missing = true;
            var roc = _missing
                ? BigInteger.Zero
                : R(
                    (100 * (value - _previous) * _previous.Sign) << 1074,
                    BigInteger.Abs(_previous)
                );
            _previous = value;
            if (_missing)
                return;
            if (_index <= period)
            {
                _firstSum += roc;
                if (_index < period)
                    return;
                _first = R(_firstSum, period);
                _firstSum = 0;
            }
            else
                _first = R(_first * (period - 2) + 2 * roc, period);
            var scaled = R(10 * _first, 1);
            var secondStart = (long)period + smooth - 1;
            if (_index <= secondStart)
            {
                _secondSum += scaled;
                if (_index < secondStart)
                    return;
                _second = R(_secondSum, smooth);
                _secondSum = 0;
            }
            else
                _second = R(_second * (smooth - 2) + 2 * scaled, smooth);
            output[0] = ExactMeanAccumulator.UnitRatio(_second, 1);
            output[2] = 1;
            var signalStart = secondStart + signal - 1;
            if (_index <= signalStart)
            {
                _signalSum += _second;
                if (_index < signalStart)
                    return;
                _signal = R(_signalSum, signal);
                _signalSum = 0;
            }
            else
                _signal = R(_signal * (signal - 1) + 2 * _second, (long)signal + 1);
            output[1] = ExactMeanAccumulator.UnitRatio(_signal, 1);
            output[3] = 1;
        }
    }
}
