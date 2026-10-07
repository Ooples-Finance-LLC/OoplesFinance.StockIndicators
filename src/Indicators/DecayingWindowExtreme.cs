using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A running close extreme decaying toward the available window mean.</summary>
/// <remarks>New or equal extremes reset age. The decay rate is
/// 1-exp(-0.1*decay*age/period), with binary64 stages. Mean and convex update
/// each round once; the result is capped by the current window extreme.
/// Zero decay gives ordinary rolling extrema. Storage grows with available history.</remarks>
public sealed class DecayingWindowExtreme : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a rolling minimum, or maximum, with finite nonnegative decay.</summary>
    public DecayingWindowExtreme(int period = 20, bool maximum = false, double decay = 0)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(decay) || decay < 0)
            throw new ArgumentOutOfRangeException(nameof(decay));
        Period = period;
        Maximum = maximum;
        Decay = decay;
    }

    /// <summary>Positive window length.</summary>
    public int Period { get; }

    /// <summary>Whether to track maxima.</summary>
    public bool Maximum { get; }

    /// <summary>Nonnegative decay intensity; zero disables decay.</summary>
    public double Decay { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Maximum, Decay);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars => DecayingExtremeReference.Calculate(bars, Period, Maximum, Decay),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(int period, bool maximum, double decay) : IIndicatorState
    {
        private readonly WindowExtremeDeque _extreme = new(period, maximum);
        private readonly Queue<double> _window = new();
        private ExactMeanAccumulator _sum;
        private double _current;
        private long _age;
        private bool _started;

        public void Reset()
        {
            _extreme.Reset();
            _window.Clear();
            _sum = default;
            _current = 0;
            _age = 0;
            _started = false;
        }

        public double Update(in Bar bar)
        {
            _extreme.Add(bar.Close);
            if (decay == 0)
                return _extreme.Value;
            if (_window.Count == period)
                _sum.Add(_window.Dequeue(), -1);
            _window.Enqueue(bar.Close);
            _sum.Add(bar.Close);
            if (_age < long.MaxValue)
                _age++;
            if (!_started || (maximum ? bar.Close >= _current : bar.Close <= _current))
            {
                _current = bar.Close;
                _age = 0;
                _started = true;
            }
            var rate = 1 - Math.Exp(-(decay * .1) * _age / period);
            var value = new ExactMeanAccumulator();
            value.Add(_current);
            value.AddProduct(_current, rate, -1);
            value.AddProduct(_sum.Mean(_window.Count), rate);
            _current = maximum
                ? Math.Min(value.Mean(1), _extreme.Value)
                : Math.Max(value.Mean(1), _extreme.Value);
            return _current;
        }
    }
}
