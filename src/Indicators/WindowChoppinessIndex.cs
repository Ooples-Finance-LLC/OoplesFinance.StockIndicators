using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Logarithmic ratio of summed true ranges to the window's true high/low span.</summary>
/// <remarks>Each true interval includes the preceding close. The first bar has no
/// interval; output starts after period intervals. A zero span is absent. Differences
/// and the rolling sum remain exact, including tiny and oversized ranges. Logarithms
/// retain ratios near one; division by log(period) and multiplication by 100 follow
/// binary64 arithmetic. Histories grow lazily.</remarks>
public sealed class WindowChoppinessIndex : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a Choppiness Index with a period of at least two.</summary>
    public WindowChoppinessIndex(int period = 14)
        : base(2)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of true intervals.</summary>
    public int Period { get; }

    /// <summary>Choppiness, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One for a complete window with a nonzero span.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        WindowChoppinessReference
                            .Calculate(bars, Period)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    new IndicatorErrorBudget(0, 4e-15, true)
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<(BigInteger High, BigInteger Low)> _intervals = new();
        private readonly double _logPeriod = Math.Log(period);
        private BigInteger _sum;
        private double _previous;
        private bool _started;

        public void Reset()
        {
            _intervals.Clear();
            _sum = 0;
            _previous = 0;
            _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_started)
            {
                _previous = bar.Close;
                _started = true;
                return;
            }
            var high = ExactVarianceWindow.Units(Math.Max(bar.High, _previous));
            var low = ExactVarianceWindow.Units(Math.Min(bar.Low, _previous));
            _previous = bar.Close;
            if (_intervals.Count == period)
            {
                var old = _intervals.Dequeue();
                _sum -= old.High - old.Low;
            }
            _intervals.Enqueue((high, low));
            _sum += high - low;
            if (_intervals.Count < period)
                return;
            foreach (var interval in _intervals)
            {
                high = BigInteger.Max(high, interval.High);
                low = BigInteger.Min(low, interval.Low);
            }
            var span = high - low;
            if (span.IsZero)
                return;
            output[0] = _sum.IsZero
                ? double.NegativeInfinity
                : 100 * (JrcWindow.LogRatio(_sum, span) / _logPeriod);
            output[1] = 1;
        }
    }
}
