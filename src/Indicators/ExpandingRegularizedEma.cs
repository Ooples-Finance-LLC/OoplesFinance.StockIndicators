using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A regularized EMA with an expanding startup alpha and two direct seed prices.</summary>
/// <remarks>The first two closes are published unchanged. Thereafter, alpha is
/// 2/(min(observations,period)+1) and the result is
/// (previous+alpha*(close-previous)+lambda*(2*previous-older))/(1+lambda).
/// The complete ratio rounds once. Period one remains regularized when lambda
/// is nonzero. Storage is constant; final overflow is rejected by the runtime.</remarks>
public sealed class ExpandingRegularizedEma
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period recurrence with finite nonnegative regularization.</summary>
    public ExpandingRegularizedEma(int period = 14, double lambda = .5)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(lambda) || lambda < 0)
            throw new ArgumentOutOfRangeException(nameof(lambda));
        Period = period;
        Lambda = lambda;
    }

    /// <summary>Maximum smoothing period after the expanding startup.</summary>
    public int Period { get; }

    /// <summary>Nonnegative regularization coefficient.</summary>
    public double Lambda { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Lambda);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        var lambda = ReferenceFraction.FromDouble(Lambda);
        var one = new ReferenceFraction(1);
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < 2)
            {
                values[i] = bars[i].Close;
                continue;
            }
            var prior = ReferenceFraction.FromDouble(values[i - 1]);
            var older = ReferenceFraction.FromDouble(values[i - 2]);
            var alpha =
                new ReferenceFraction(2) / new ReferenceFraction(Math.Min((long)i + 1, Period) + 1);
            values[i] = (
                (
                    prior
                    + alpha * (ReferenceFraction.FromDouble(bars[i].Close) - prior)
                    + lambda * (new ReferenceFraction(2) * prior - older)
                ) / (one + lambda)
            ).ToDouble();
            if (double.IsInfinity(values[i]))
                break;
        }
        return values;
    }

    internal sealed class State(int period, double lambda) : IIndicatorState
    {
        private int _count, _denominatorPeriod;
        private JurikDyadic _denominator;
        private double _previous,
            _older;

        public void Reset()
        {
            _count = _denominatorPeriod = 0;
            _denominator = default;
            _previous = _older = 0;
        }

        public double Update(in Bar bar)
        {
            if (_count < Math.Max(3, period))
                _count++;
            var result = bar.Close;
            if (_count > 2)
            {
                var effective = Math.Min(_count, period);
                if (_denominatorPeriod != effective)
                {
                    _denominator = JurikDyadic.FromDouble(lambda);
                    _denominator.Add(1);
                    _denominator.Multiply((double)effective + 1);
                    _denominatorPeriod = effective;
                }
                var sum = JurikDyadic.FromDouble(_previous);
                sum.Multiply(effective - 1);
                var current = JurikDyadic.FromDouble(bar.Close);
                current.Multiply(2);
                sum.AddExact(current);
                var regularization = JurikDyadic.FromDouble(_previous);
                regularization.Multiply(2);
                regularization.Subtract(JurikDyadic.FromDouble(_older));
                regularization.Multiply(lambda);
                regularization.Multiply((double)effective + 1);
                sum.AddExact(regularization);
                result = sum.Ratio(_denominator);
            }
            _older = _previous;
            _previous = result;
            return result;
        }
    }
}
