using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Percentage change with an explicit flag for unavailable results.</summary>
/// <remarks>Value is zero and IsDefined is zero during startup or when the earlier close is zero.
/// Otherwise IsDefined is one and Value is 100 times (current - previous) / previous.
/// The complete formula is rounded once; the runtime rejects mathematically unrepresentable results.
/// Storage grows with observed history, up to the requested period.</remarks>
public sealed class RateOfChangeWithValidity
    : MultiOutputIndicatorBase,
        IMomentumIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a percentage change over the specified positive lag.</summary>
    public RateOfChangeWithValidity(int period = 1)
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        (Value, IsDefined) = DeclaredOutputs;
    }

    /// <summary>Number of bars separating the closes.</summary>
    public int Period { get; }

    /// <summary>Percentage change, or a zero placeholder when IsDefined is zero.</summary>
    public IIndicatorOutput Value { get; }

    /// <summary>One for a defined percentage change; zero for missing history or a zero denominator.</summary>
    public IIndicatorOutput IsDefined { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => Reference(bars, false),
                IndicatorErrorBudget.Exact
            ),
            IndicatorValidationRule.Reference(1, bars => Reference(bars, true), 0, 0),
            IndicatorValidationRule.Bounds(1, 0, 1),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars, bool validity)
    {
        var values = new double[bars.Count];
        for (var i = Period; i < bars.Count; i++)
        {
            if (bars[i - Period].Close == 0)
                continue;
            var previous = ReferenceFraction.FromDouble(bars[i - Period].Close);
            values[i] = validity
                ? 1
                : (
                    (ReferenceFraction.FromDouble(bars[i].Close) - previous)
                    / previous
                    * new ReferenceFraction(100)
                ).ToDouble();
        }
        return values;
    }

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<double> _history = new();

        public void Reset() => _history.Clear();

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            if (_history.Count < period)
            {
                _history.Enqueue(bar.Close);
                return;
            }
            var previous = _history.Dequeue();
            _history.Enqueue(bar.Close);
            if (previous == 0)
                return;
            var numerator = new ExactMeanAccumulator();
            numerator.Add(bar.Close, 100);
            numerator.Add(previous, -100);
            var denominator = new ExactMeanAccumulator();
            denominator.Add(previous);
            outputs[0] = numerator.Ratio(denominator);
            outputs[1] = 1;
        }
    }
}
