using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window log-return sample deviation or root mean square.</summary>
/// <remarks>Log returns round once using certified intervals, moments remain exact,
/// and the final root (including optional 252-day annualization) rounds once.
/// A zero previous price skips the return. Historical mode keeps reading the
/// existing window on that bar; realized mode publishes zero. Before a complete
/// window both publish zero. A nonpositive price ratio consumes an undefined
/// return: historical mode recovers when it leaves the window, while realized
/// mode remains undefined thereafter, matching its poisoned rolling sum.
/// Histories grow lazily and period-independent startup needs no large allocation.</remarks>
public sealed class WindowLogVolatility : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a period-at-least-two volatility calculation.</summary>
    public WindowLogVolatility(int period = 20, bool realized = false, bool annualized = true)
        : base(2)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        Realized = realized;
        Annualized = annualized;
    }

    /// <summary>Number of retained returns.</summary>
    public int Period { get; }

    /// <summary>Whether to calculate RMS instead of sample deviation.</summary>
    public bool Realized { get; }

    /// <summary>Whether to scale the variance by 252 before taking its root.</summary>
    public bool Annualized { get; }

    /// <summary>Volatility or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>Presence; startup zero is present, undefined full windows are absent.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Realized, Annualized);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        LogVolatilityReference
                            .Values(bars, Period, Realized, Annualized)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, bool realized, bool annualized) : IMultiOutputState
    {
        private readonly Queue<BigInteger?> _returns = new();
        private double _previous;
        private BigInteger _sum,
            _squares;
        private int _missing;
        private bool _poisoned;

        public void Reset()
        {
            _returns.Clear();
            _previous = 0;
            _sum = _squares = 0;
            _missing = 0;
            _poisoned = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            output[1] = 1;
            var value = bar.Close;
            var prior = _previous;
            _previous = value;
            if (prior != 0) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
            {
                BigInteger? next =
                    value != 0 && Math.Sign(value) == Math.Sign(prior) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
                        ? ExactVarianceWindow.Units(CertifiedLogReturn.Of(value, prior))
                        : null;
                if (_returns.Count == period)
                {
                    var old = _returns.Dequeue();
                    if (old.HasValue)
                    {
                        _sum -= old.Value;
                        _squares -= old.Value * old.Value;
                    }
                    else
                        _missing--;
                }
                _returns.Enqueue(next);
                if (next.HasValue)
                {
                    _sum += next.Value;
                    _squares += next.Value * next.Value;
                }
                else
                {
                    _missing++;
                    _poisoned = true;
                }
            }
            else if (realized)
                return;
            if (_returns.Count < period)
                return;
            if (_missing > 0 || (realized && _poisoned))
            {
                output[1] = 0;
                return;
            }
            var numerator = realized ? _squares : period * _squares - _sum * _sum;
            var denominator = realized ? (BigInteger)period : (BigInteger)period * (period - 1);
            output[0] = ExactPopulationDeviation.RootRatio(
                numerator * (annualized ? 252 : 1),
                denominator
            );
        }
    }
}
