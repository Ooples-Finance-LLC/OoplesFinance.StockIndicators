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

    internal sealed class State(int period, int smooth, int signal) : IMultiOutputState
    {
        private long _index = -1;
        private double _previous;
        private JurikDyadic _first,
            _second,
            _signal,
            _firstSum,
            _secondSum,
            _signalSum;
        private bool _missing;

        private static JurikDyadic R(JurikDyadic numerator, JurikDyadic denominator)
        {
            // Stored stages round to binary64 precision but retain upper exponents.
            for (int shift = 0; ; shift += 1024)
            {
                var value = numerator.Ratio(denominator);
                if (!double.IsInfinity(value))
                {
                    var result = JurikDyadic.FromDouble(value);
                    if (shift != 0) result.ScaleByPowerOfTwo(shift);
                    return result;
                }
                numerator.ScaleByPowerOfTwo(-1024);
            }
        }

        private static JurikDyadic R(JurikDyadic numerator, long divisor) =>
            R(numerator, JurikDyadic.FromDouble(divisor));

        private static JurikDyadic Smooth(JurikDyadic previous, JurikDyadic current, int priorWeight, long divisor)
        {
            previous.Multiply(priorWeight);
            current.Multiply(2);
            previous.AddExact(current);
            return R(previous, divisor);
        }

        public void Reset()
        {
            _index = -1;
            _previous = 0;
            _first = _second = _signal = _firstSum = _secondSum = _signalSum = default;
            _missing = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var value = bar.Close;
            if (++_index == 0)
            {
                _previous = value;
                return;
            }
            if (_previous == 0)
                _missing = true;
            var previous = _previous;
            _previous = value;
            if (_missing)
                return;
            var roc = JurikDyadic.FromDouble(value);
            roc.Subtract(JurikDyadic.FromDouble(previous));
            roc.Multiply(100);
            roc = R(roc, JurikDyadic.FromDouble(previous));
            if (_index <= period)
            {
                _firstSum.AddExact(roc);
                if (_index < period)
                    return;
                _first = R(_firstSum, period);
                _firstSum = default;
            }
            else
                _first = Smooth(_first, roc, period - 2, period);
            var scaled = _first;
            scaled.Multiply(10);
            scaled = scaled.Rounded();
            var secondStart = (long)period + smooth - 1;
            if (_index <= secondStart)
            {
                _secondSum.AddExact(scaled);
                if (_index < secondStart)
                    return;
                _second = R(_secondSum, smooth);
                _secondSum = default;
            }
            else
                _second = Smooth(_second, scaled, smooth - 2, smooth);
            output[0] = _second.Mean(1);
            output[2] = 1;
            var signalStart = secondStart + signal - 1;
            if (_index <= signalStart)
            {
                _signalSum.AddExact(_second);
                if (_index < signalStart)
                    return;
                _signal = R(_signalSum, signal);
                _signalSum = default;
            }
            else
                _signal = Smooth(_signal, _second, signal - 1, (long)signal + 1);
            output[1] = _signal.Mean(1);
            output[3] = 1;
        }
    }
}
