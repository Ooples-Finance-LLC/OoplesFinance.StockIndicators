using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Two mean-seeded EMA stages of signed/absolute changes with a matched early signal.</summary>
/// <remarks>Price differences, means and EMA recurrences round once with extended
/// upper exponents. Their final percent ratio rounds once. Zero absolute mean is
/// absent. For smoothing greater than one, TSI seeds at period+smoothPeriod-1.
/// Smoothing one starts one bar after the first mean, without a second seed.
/// Signal zero disables output; signal one stays absent. Larger signals use a
/// fixed-size mean seed and EMA, preserving the shortened smoothing-one startup.
/// A missing seed input or later TSI makes the signal permanently absent.
/// Native present-NaN startup results are represented as absence.</remarks>
public sealed class SeededTrueStrength : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates TSI with positive smoothing periods and nonnegative signal period.</summary>
    public SeededTrueStrength(int period = 25, int smoothPeriod = 13, int signalPeriod = 7)
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (smoothPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(smoothPeriod));
        if (signalPeriod < 0)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        Period = period;
        SmoothPeriod = smoothPeriod;
        SignalPeriod = signalPeriod;
    }

    /// <summary>First EMA period.</summary>
    public int Period { get; }

    /// <summary>Second EMA period.</summary>
    public int SmoothPeriod { get; }

    /// <summary>Signal period; zero disables and one never seeds.</summary>
    public int SignalPeriod { get; }

    /// <summary>True strength or zero when absent.</summary>
    public IIndicatorOutput Strength => Outputs[0];

    /// <summary>Signal or zero when absent.</summary>
    public IIndicatorOutput Signal => Outputs[1];

    /// <summary>Strength presence.</summary>
    public IIndicatorOutput StrengthIsDefined => Outputs[2];

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
                            .Tsi(bars, Period, SmoothPeriod, SignalPeriod)[slot % 2]
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
            _signed,
            _absolute,
            _secondSigned,
            _secondAbsolute,
            _sumSigned,
            _sumAbsolute,
            _seedSignal;
        private double? _signal;
        private bool _signalMissing;

        private static BigInteger R(BigInteger x, BigInteger divisor) =>
            RocBankValue.RoundUnits(x, divisor);

        public void Reset()
        {
            _index = -1;
            _previous =
                _signed =
                _absolute =
                _secondSigned =
                _secondAbsolute =
                _sumSigned =
                _sumAbsolute =
                _seedSignal =
                    0;
            _signal = null;
            _signalMissing = false;
        }

        private static void Publish(double? value, int slot, Span<double> output)
        {
            if (value.HasValue)
            {
                output[slot] = value.Value;
                output[slot + 2] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var value = ExactVarianceWindow.Units(bar.Close);
            var change = R(value - _previous, 1);
            _previous = value;
            if (++_index == 0)
                return;
            if (_index <= period)
            {
                _sumSigned += change;
                _sumAbsolute += BigInteger.Abs(change);
                if (_index == period)
                {
                    _signed = R(_sumSigned, period);
                    _absolute = R(_sumAbsolute, period);
                    _sumSigned = _signed;
                    _sumAbsolute = _absolute;
                }
                return;
            }
            _signed = R(_signed * (period - 1) + 2 * change, (long)period + 1);
            _absolute = R(_absolute * (period - 1) + 2 * BigInteger.Abs(change), (long)period + 1);
            var secondStart = (long)period + smooth - 1;
            if (_index <= secondStart)
            {
                _sumSigned += _signed;
                _sumAbsolute += _absolute;
                if (_index < secondStart)
                    return;
                _secondSigned = R(_sumSigned, smooth);
                _secondAbsolute = R(_sumAbsolute, smooth);
                _sumSigned = _sumAbsolute = 0;
            }
            else
            {
                _secondSigned = R(_secondSigned * (smooth - 1) + 2 * _signed, (long)smooth + 1);
                _secondAbsolute = R(
                    _secondAbsolute * (smooth - 1) + 2 * _absolute,
                    (long)smooth + 1
                );
            }
            double? tsi = _secondAbsolute.IsZero
                ? null
                : ExactMeanAccumulator.UnitRatio((100 * _secondSigned) << 1074, _secondAbsolute);
            Publish(tsi, 0, output);
            if (_index == secondStart)
            {
                _signalMissing = !tsi.HasValue;
                _seedSignal = tsi.HasValue ? ExactVarianceWindow.Units(tsi.Value) : 0;
                return;
            }
            if (signal <= 1)
                return;
            var seedIndex = secondStart + signal - 1;
            if (_index <= seedIndex)
            {
                if (tsi.HasValue)
                    _seedSignal += ExactVarianceWindow.Units(tsi.Value);
                else
                    _signalMissing = true;
                if (_index == seedIndex && !_signalMissing)
                    _signal = ExactMeanAccumulator.UnitRatio(_seedSignal, signal);
            }
            else if (_signal.HasValue && tsi.HasValue)
                _signal = ExactMeanAccumulator.UnitRatio(
                    ExactVarianceWindow.Units(_signal.Value) * (signal - 1)
                        + 2 * ExactVarianceWindow.Units(tsi.Value),
                    (long)signal + 1
                );
            else
                _signal = null;
            Publish(_signal, 1, output);
        }
    }
}
