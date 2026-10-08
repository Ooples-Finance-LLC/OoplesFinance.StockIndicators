using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Fisher transform of a smoothed rolling range position, with a one-bar trigger.</summary>
/// <remarks>Uses available windows, publishes zero initially, and omits the first trigger.
/// Flat windows reset the range-position state to zero. Position uses an exact ratio;
/// binary64 smoothing and Math.Log define the bounded transform. Values beyond +/-0.99
/// clamp to +/-0.999. The default input is the high/low midpoint; close input is optional.</remarks>
public sealed class WindowFisherTransform : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period Fisher transform.</summary>
    public WindowFisherTransform(int period = 10, bool useMidpoint = true)
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        UseMidpoint = useMidpoint;
    }

    /// <summary>Rolling range period.</summary>
    public int Period { get; }

    /// <summary>Whether to use the high/low midpoint rather than close.</summary>
    public bool UseMidpoint { get; }

    /// <summary>Fisher transform.</summary>
    public IIndicatorOutput Fisher => Outputs[0];

    /// <summary>Previous Fisher value.</summary>
    public IIndicatorOutput Trigger => Outputs[1];

    /// <summary>Fisher presence.</summary>
    public IIndicatorOutput IsFisherDefined => Outputs[2];

    /// <summary>Trigger presence.</summary>
    public IIndicatorOutput IsTriggerDefined => Outputs[3];

    /// <inheritdoc/>
    public override int WarmupBars => 0;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, UseMidpoint);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        WindowFisherReference
                            .Values(bars, Period, UseMidpoint)[slot % 2]
                            .Select(v =>
                                slot < 2 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, bool midpoint) : IMultiOutputState
    {
        private readonly Queue<double> _prices = new();
        private double _position,
            _fisher;
        private bool _started;

        public void Reset()
        {
            _prices.Clear();
            _position = _fisher = 0;
            _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            output[2] = 1;
            var price = midpoint
                ? ExactMeanAccumulator.UnitRatio(
                    ExactVarianceWindow.Units(bar.High) + ExactVarianceWindow.Units(bar.Low),
                    2
                )
                : bar.Close;
            if (_prices.Count == period)
                _prices.Dequeue();
            _prices.Enqueue(price);
            if (!_started)
            {
                _started = true;
                return;
            }
            var low = _prices.Min();
            var high = _prices.Max();
            var range = ExactVarianceWindow.Units(high) - ExactVarianceWindow.Units(low);
            var ratio = range.IsZero
                ? 0
                : ExactMeanAccumulator.UnitRatio(
                    (ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(low)) << 1074,
                    range
                );
            _position = range.IsZero ? 0 : .33 * 2 * (ratio - .5) + .67 * _position;
            if (_position > .99)
                _position = .999;
            if (_position < -.99)
                _position = -.999;
            output[1] = _fisher;
            output[3] = 1;
            _fisher = .5 * Math.Log((1 + _position) / (1 - _position)) + .5 * _fisher;
            output[0] = _fisher;
        }
    }
}
