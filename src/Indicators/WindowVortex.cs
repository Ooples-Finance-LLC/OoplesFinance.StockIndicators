using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window positive and negative Vortex movement divided by summed true range.</summary>
/// <remarks>The first candle establishes prior high/low/close; output starts at
/// index period. Zero total range is absent. Differences and sums remain exact
/// until each final ratio rounds once. Histories grow lazily.</remarks>
public sealed class WindowVortex : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates Vortex ratios with a positive window length.</summary>
    public WindowVortex(int period = 14)
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of movements and true ranges.</summary>
    public int Period { get; }

    /// <summary>Positive ratio, or zero when absent.</summary>
    public IIndicatorOutput Positive => Outputs[0];

    /// <summary>Negative ratio, or zero when absent.</summary>
    public IIndicatorOutput Negative => Outputs[1];

    /// <summary>Positive ratio presence.</summary>
    public IIndicatorOutput PositiveIsDefined => Outputs[2];

    /// <summary>Negative ratio presence.</summary>
    public IIndicatorOutput NegativeIsDefined => Outputs[3];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
#if !NETFRAMEWORK
        new CompactState(Period);
#else
        new State(Period);
#endif

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => TrueRangeRatioReference.Vortex(bars, Period)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

#if !NETFRAMEWORK
    internal sealed class CompactState(int period, int count = 0) : IMultiOutputState
    {
        private readonly Queue<(JurikDyadic Range, JurikDyadic Positive, JurikDyadic Negative)> _window
            = new(Math.Min(period, Math.Max(0, count - 1)));
        private JurikDyadic _range, _positive, _negative;
        private Bar _previous;
        private bool _started;

        public void Reset()
        {
            _window.Clear(); _range = _positive = _negative = default;
            _previous = default; _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_started) { _started = true; _previous = bar; return; }
            JurikDyadic range;
            if (bar.High >= bar.Low)
            {
                // For ordered candles the maximum true interval is determined
                // from endpoints without rounding any of the differences.
                range = Difference(Math.Max(bar.High, _previous.Close), Math.Min(bar.Low, _previous.Close));
            }
            else
            {
                // Inverted finite candles retain the original definition:
                // high-low is negative, leaving the two absolute gaps.
                range = AbsoluteDifference(bar.High, _previous.Close);
                var other = AbsoluteDifference(bar.Low, _previous.Close);
                var comparison = range; comparison.Subtract(other);
                if (comparison.Sign < 0) range = other;
            }
            var positive = AbsoluteDifference(bar.High, _previous.Low);
            var negative = AbsoluteDifference(bar.Low, _previous.High);
            _previous = bar;
            if (_window.Count == period)
            {
                var old = _window.Dequeue();
                _range.Subtract(old.Range); _positive.Subtract(old.Positive); _negative.Subtract(old.Negative);
            }
            _window.Enqueue((range, positive, negative));
            _range.AddExact(range); _positive.AddExact(positive); _negative.AddExact(negative);
            if (_window.Count < period || _range.Sign == 0) return;
            output[0] = _positive.Ratio(_range);
            output[1] = _negative.Ratio(_range);
            output[2] = output[3] = 1;
        }

        private static JurikDyadic Difference(double a, double b)
        {
            var value = JurikDyadic.FromDouble(a);
            value.Subtract(JurikDyadic.FromDouble(b));
            return value;
        }

        private static JurikDyadic AbsoluteDifference(double a, double b) =>
            a >= b ? Difference(a, b) : Difference(b, a);
    }
#else
    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<(
            BigInteger Range,
            BigInteger Positive,
            BigInteger Negative
        )> _window = new();
        private BigInteger _range,
            _positive,
            _negative;
        private Bar _previous;
        private bool _started;

        public void Reset()
        {
            _window.Clear();
            _range = _positive = _negative = 0;
            _previous = default;
            _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_started)
            {
                _started = true;
                _previous = bar;
                return;
            }
            var h = ExactVarianceWindow.Units(bar.High);
            var l = ExactVarianceWindow.Units(bar.Low);
            var c = ExactVarianceWindow.Units(_previous.Close);
            var range = BigInteger.Max(
                h - l,
                BigInteger.Max(BigInteger.Abs(h - c), BigInteger.Abs(l - c))
            );
            var positive = BigInteger.Abs(h - ExactVarianceWindow.Units(_previous.Low));
            var negative = BigInteger.Abs(l - ExactVarianceWindow.Units(_previous.High));
            _previous = bar;
            if (_window.Count == period)
            {
                var old = _window.Dequeue();
                _range -= old.Range;
                _positive -= old.Positive;
                _negative -= old.Negative;
            }
            _window.Enqueue((range, positive, negative));
            _range += range;
            _positive += positive;
            _negative += negative;
            if (_window.Count < period || _range.IsZero)
                return;
            output[0] = ExactMeanAccumulator.UnitRatio(_positive << 1074, _range);
            output[1] = ExactMeanAccumulator.UnitRatio(_negative << 1074, _range);
            output[2] = output[3] = 1;
        }
    }
#endif
}
