using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Klinger volume-force oscillator and independently seeded EMA signal.</summary>
/// <remarks>The first two candles initialize direction and range history. Complete
/// force and EMA stages round once with extended upper exponents; only published
/// overflow is rejected. Exact HLC comparisons preserve direction and range branches.
/// Missing outputs publish zero with a separate presence flag. State storage is constant.</remarks>
public sealed class KlingerVolumeWithSignal : MultiOutputIndicatorBase, IVolumeIndicator, IIndicatorValidationContract
{
    /// <summary>Creates ordered fast/slow periods (fast at least three) and a positive signal period.</summary>
    public KlingerVolumeWithSignal(int fastPeriod = 34, int slowPeriod = 55, int signalPeriod = 13) : base(4)
    {
        if (fastPeriod < 3) throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod <= fastPeriod) throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        if (signalPeriod < 1) throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        FastPeriod = fastPeriod; SlowPeriod = slowPeriod; SignalPeriod = signalPeriod;
    }
    /// <summary>Fast SMA-seeded EMA period.</summary>
    public int FastPeriod { get; }
    /// <summary>Slow SMA-seeded EMA period.</summary>
    public int SlowPeriod { get; }
    /// <summary>Signal SMA-seeded EMA period.</summary>
    public int SignalPeriod { get; }
    /// <summary>Fast-minus-slow force EMA, or zero when absent.</summary>
    public IIndicatorOutput Oscillator => Outputs[0];
    /// <summary>EMA of available oscillator values, or zero when absent.</summary>
    public IIndicatorOutput Signal => Outputs[1];
    /// <summary>One when the oscillator exists.</summary>
    public IIndicatorOutput OscillatorIsDefined => Outputs[2];
    /// <summary>One when the signal exists.</summary>
    public IIndicatorOutput SignalIsDefined => Outputs[3];
    /// <inheritdoc/>
    protected internal override object CreateState() => new State(FastPeriod, SlowPeriod, SignalPeriod);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 4).Select(slot =>
        IndicatorValidationRule.ReferenceWithOverflowRejection(slot, bars =>
            KlingerVolumeSnapshot.CalculateCore(bars, FastPeriod, SlowPeriod, SignalPeriod, rejectOverflow: false).Select(value => slot switch
            {
                0 => value.Oscillator ?? 0,
                1 => value.Signal ?? 0,
                2 => value.Oscillator.HasValue ? 1d : 0,
                _ => value.Signal.HasValue ? 1d : 0
            }).ToArray(), IndicatorErrorBudget.Exact));

    // Preserve binary64 significands above its exponent range. Scaling only occurs
    // after overflow; the first finite quotient is normal, so restoration adds no rounding.
    private static JurikDyadic RoundedRatio(JurikDyadic numerator, JurikDyadic denominator)
    {
        int shift = 0;
        while (true)
        {
            double value = numerator.Ratio(denominator);
            if (FrameworkCompatibility.IsFinite(value))
            {
                var result = JurikDyadic.FromDouble(value);
                result.ScaleByPowerOfTwo(shift); return result;
            }
            numerator.ScaleByPowerOfTwo(-32); shift += 32;
        }
    }

    private sealed class Average(int period)
    {
        private int _count;
        private JurikDyadic _sum, _value;
        internal void Reset() { _count = 0; _sum = _value = default; }
        internal bool Add(JurikDyadic input, out JurikDyadic value)
        {
            if (_count < period)
            {
                _sum.AddExact(input); _count++;
                if (_count < period) { value = default; return false; }
                _value = RoundedRatio(_sum, JurikDyadic.FromDouble(period));
                _sum = default;
            }
            else
            {
                var next = _value; next.Multiply(period - 1);
                input.Multiply(2); next.AddExact(input);
                _value = RoundedRatio(next, JurikDyadic.FromDouble((long)period + 1));
            }
            value = _value; return true;
        }
    }

    internal sealed class State(int fastPeriod, int slowPeriod, int signalPeriod) : IMultiOutputState
    {
        private readonly Average _fast = new(fastPeriod), _slow = new(slowPeriod), _signal = new(signalPeriod);
        private JurikDyadic _previousBasis, _previousRange, _cumulative, _force;
        private int _seen, _direction;
        public void Reset()
        {
            _fast.Reset(); _slow.Reset(); _signal.Reset();
            _previousBasis = _previousRange = _cumulative = _force = default;
            _seen = _direction = 0;
        }
        public void Update(in Bar bar, Span<double> output)
        {
            if (!FrameworkCompatibility.IsFinite(bar.High) || !FrameworkCompatibility.IsFinite(bar.Low)
                || !FrameworkCompatibility.IsFinite(bar.Close) || !FrameworkCompatibility.IsFinite(bar.Volume))
                throw new ArgumentOutOfRangeException(nameof(bar));
            output.Clear();
            var high = JurikDyadic.FromDouble(bar.High);
            var low = JurikDyadic.FromDouble(bar.Low);
            var basis = high; basis.AddExact(low); basis.Add(bar.Close);
            var difference = basis; difference.Subtract(_previousBasis);
            int direction = difference.Sign > 0 ? 1 : -1;
            var range = high; range.Subtract(low);
            if (_seen == 2)
            {
                if (direction != _direction) _cumulative = _previousRange;
                _cumulative.AddExact(range);
                var residual = range; residual.Subtract(_cumulative);
                var volume = JurikDyadic.FromDouble(bar.Volume);
                if (residual.Sign == 0 || volume.Sign == 0) _force = default;
                else if (range.Sign == 0)
                {
                    volume.Multiply(200 * direction); _force = volume.Rounded();
                }
                else if (_cumulative.Sign != 0)
                {
                    if (residual.Sign < 0) residual.Multiply(-1);
                    var denominator = _cumulative;
                    if (denominator.Sign < 0) denominator.Multiply(-1);
                    residual.Multiply(volume); residual.Multiply(200 * direction);
                    _force = RoundedRatio(residual, denominator);
                }
                // A zero cumulative range otherwise retains the preceding force.
                bool fastReady = _fast.Add(_force, out var fast);
                bool slowReady = _slow.Add(_force, out var slow);
                if (fastReady && slowReady)
                {
                    fast.Subtract(slow); var oscillator = fast.Rounded();
                    bool signalReady = _signal.Add(oscillator, out var signal);
                    output[0] = oscillator.Mean(1); output[2] = 1;
                    if (signalReady) { output[1] = signal.Mean(1); output[3] = 1; }
                }
            }
            else _seen++;
            _previousBasis = basis; _previousRange = range; _direction = direction;
        }
    }
}
