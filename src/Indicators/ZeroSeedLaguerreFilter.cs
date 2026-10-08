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

    private sealed class State(double gamma) : IIndicatorState
    {
        private readonly RocBankValue[] _stages = new RocBankValue[4];

        public void Reset() => Array.Clear(_stages, 0, _stages.Length);

        private static void Product(
            ref ExactMeanAccumulator sum,
            RocBankValue value,
            double coefficient
        )
        {
            var product = new ExactMeanAccumulator();
            product.AddProduct(value.Mantissa, -coefficient);
            product.ScaleByPowerOfTwo(value.UpperShift);
            sum.Subtract(product);
        }

        public double Update(in Bar bar)
        {
            var sum = new ExactMeanAccumulator();
            sum.Add(bar.Close);
            sum.AddProduct(bar.Close, -gamma);
            Product(ref sum, _stages[0], gamma);
            var current = RocBankValue.Round(sum);
            var prior = _stages[0];
            _stages[0] = current;
            for (var stage = 1; stage < 4; stage++)
            {
                var next = new ExactMeanAccumulator();
                prior.AddTo(ref next);
                Product(ref next, _stages[stage], gamma);
                Product(ref next, current, -gamma);
                prior = _stages[stage];
                current = RocBankValue.Round(next);
                _stages[stage] = current;
            }
            var output = new ExactMeanAccumulator();
            for (var stage = 0; stage < 4; stage++)
                _stages[stage].AddTo(ref output, stage is 1 or 2 ? 2 : 1);
            return output.Mean(6);
        }
    }
}
