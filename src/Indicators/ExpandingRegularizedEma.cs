using System.Numerics;
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

    private sealed class State(int period, double lambda) : IIndicatorState
    {
        private int _count;
        private double _previous,
            _older;

        public void Reset()
        {
            _count = 0;
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
                var sum = new ExactMeanAccumulator();
                sum.Add(_previous, effective - 1);
                sum.Add(bar.Close, 2);
                // Multiply by effective+1 without narrowing a maximum-period divisor.
                for (var copy = 0; copy < 2; copy++)
                {
                    sum.AddProduct(_previous, lambda, effective);
                    sum.AddProduct(_previous, lambda);
                }
                sum.AddProduct(_older, lambda, -effective);
                sum.AddProduct(_older, lambda, -1);
                var denominator = new ExactMeanAccumulator();
                var divisor = new BigInteger(effective) + 1;
                denominator.Add(1, divisor);
                denominator.Add(lambda, divisor);
                result = sum.Ratio(denominator);
            }
            _older = _previous;
            _previous = result;
            return result;
        }
    }
}
