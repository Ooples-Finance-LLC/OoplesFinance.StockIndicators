using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Four-stage Laguerre filter with zero-initialized stages.</summary>
/// <remarks>Uses gamma in [0,1], default .1, and publishes (L0+2L1+2L2+L3)/6.
/// Startup is published from the first observation. Gamma zero is a four-tap
/// [1,2,2,1]/6 filter; gamma one retains the zero seed. Each complete stage is
/// rounded once to binary64 precision with an extended upper exponent so an
/// unpublished overflow cannot destroy a finite final result. The final ratio
/// is rounded once. This seed convention differs from the price-seeded filter.</remarks>
public sealed class ZeroSeedLaguerreFilter : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a zero-seeded filter with finite gamma in [0,1].</summary>
    public ZeroSeedLaguerreFilter(double gamma = .1)
    {
        if (!FrameworkCompatibility.IsFinite(gamma) || gamma < 0 || gamma > 1)
            throw new ArgumentOutOfRangeException(nameof(gamma));
        Gamma = gamma;
    }

    /// <summary>Laguerre recurrence coefficient.</summary>
    public double Gamma { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 3;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Gamma);

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
        var result = new double[bars.Count];
        var gamma = ReferenceFraction.FromDouble(Gamma);
        var one = new ReferenceFraction(1);
        var prior = Enumerable.Repeat(new ReferenceFraction(0), 4).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var next = new ReferenceFraction[4];
            next[0] = (
                (one - gamma) * ReferenceFraction.FromDouble(bars[i].Close) + gamma * prior[0]
            ).RoundExtendedBinary64();
            for (var stage = 1; stage < 4; stage++)
                next[stage] = (
                    prior[stage - 1] + gamma * (prior[stage] - next[stage - 1])
                ).RoundExtendedBinary64();
            result[i] = (
                (
                    next[0]
                    + new ReferenceFraction(2) * next[1]
                    + new ReferenceFraction(2) * next[2]
                    + next[3]
                ) / new ReferenceFraction(6)
            ).ToDouble();
            prior = next;
        }
        return result;
    }

    internal sealed class State(double gamma) : IIndicatorState
    {
        private readonly JurikDyadic[] _stages = new JurikDyadic[4];

        public void Reset() => Array.Clear(_stages, 0, _stages.Length);

        public double Update(in Bar bar)
        {
            var price = JurikDyadic.FromDouble(bar.Close);
            var difference = _stages[0];
            difference.Subtract(price);
            // Exact differences feed one rounded stage update. SumProduct uses
            // FMA only when both operands are exactly representable doubles;
            // wider intermediates retain the established extended exponent.
            var current = JurikDyadic.SumProduct(price, difference, gamma);
            var prior = _stages[0];
            _stages[0] = current;
            for (var stage = 1; stage < 4; stage++)
            {
                difference = _stages[stage];
                difference.Subtract(current);
                var next = JurikDyadic.SumProduct(prior, difference, gamma);
                prior = _stages[stage];
                current = next;
                _stages[stage] = current;
            }
            var output = _stages[0];
            for (var stage = 1; stage < 4; stage++)
            {
                var term = _stages[stage];
                if (stage < 3) term.Multiply(2);
                output.AddExact(term);
            }
            return output.Mean(6);
        }
    }
}
