using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Zero-seeded EMA normalized by its accumulated startup mass.</summary>
/// <remarks>State retains a normalized mean and binary64 mass. The next weights
/// are alpha and (1-alpha)*previousMass; the exact weighted ratio rounds once,
/// then mass rounds once. This avoids cancellation in 1-residual for tiny alpha.
/// A separate binary64 residual follows the Quan EMA 1e-10 cutoff: compensation
/// ends on the update after residual crosses the threshold. That update publishes
/// the unnormalized sum and resets mass to one. All finite alpha in (0,1] are supported.</remarks>
public sealed class MassNormalizedEma : IndicatorBase, IMovingAverage, IIndicatorValidationContract
{
    /// <summary>Creates a compensated average with alpha=2/(period+1).</summary>
    public MassNormalizedEma(int period = 20)
        : this(AlphaFor(period)) { }

    /// <summary>Creates a compensated average with explicit finite alpha in (0,1].</summary>
    public MassNormalizedEma(double alpha)
    {
        ValidateAlpha(alpha, nameof(alpha));
        Alpha = alpha;
    }

    /// <summary>Smoothing weight.</summary>
    public double Alpha { get; }

    internal static double AlphaFor(int period)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        return 2d / ((long)period + 1);
    }

    internal static void ValidateAlpha(double alpha, string name)
    {
        if (!FrameworkCompatibility.IsFinite(alpha) || alpha <= 0 || alpha > 1)
            throw new ArgumentOutOfRangeException(name);
    }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Alpha);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars => MassNormalizedReference.Calculate(bars, [Alpha]),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(double alpha) : IIndicatorState
    {
        private readonly MassNormalizedStage _stage = new(alpha);

        public void Reset() => _stage.Reset();

        public double Update(in Bar bar) => _stage.Next(bar.Close);
    }
}

/// <summary>Four independently compensated EMA stages combined as 4*E1-6*E2+4*E3-E4.</summary>
/// <remarks>Each stage uses MassNormalizedEma's numerical convention. The final
/// combination rounds once; genuine final overflow is rejected. Reset clears all stages.</remarks>
public sealed class MassNormalizedQuadrupleEma
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates four compensated stages with finite smoothing weights in (0,1].</summary>
    public MassNormalizedQuadrupleEma(
        double alpha1 = .2,
        double alpha2 = .2,
        double alpha3 = .2,
        double alpha4 = .2
    )
    {
        MassNormalizedEma.ValidateAlpha(alpha1, nameof(alpha1));
        MassNormalizedEma.ValidateAlpha(alpha2, nameof(alpha2));
        MassNormalizedEma.ValidateAlpha(alpha3, nameof(alpha3));
        MassNormalizedEma.ValidateAlpha(alpha4, nameof(alpha4));
        Alpha1 = alpha1;
        Alpha2 = alpha2;
        Alpha3 = alpha3;
        Alpha4 = alpha4;
    }

    /// <summary>First stage weight.</summary>
    public double Alpha1 { get; }

    /// <summary>Second stage weight.</summary>
    public double Alpha2 { get; }

    /// <summary>Third stage weight.</summary>
    public double Alpha3 { get; }

    /// <summary>Fourth stage weight.</summary>
    public double Alpha4 { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State([Alpha1, Alpha2, Alpha3, Alpha4]);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => MassNormalizedReference.Calculate(bars, [Alpha1, Alpha2, Alpha3, Alpha4]),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(double[] alphas) : IIndicatorState
    {
        private readonly MassNormalizedStage[] _stages = alphas
            .Select(a => new MassNormalizedStage(a))
            .ToArray();

        public void Reset()
        {
            foreach (var stage in _stages)
                stage.Reset();
        }

        public double Update(in Bar bar)
        {
            var value = bar.Close;
            var sum = new ExactMeanAccumulator();
            for (var i = 0; i < 4; i++)
            {
                value = _stages[i].Next(value);
                sum.Add(
                    value,
                    i == 0 || i == 2 ? 4
                        : i == 1 ? -6
                        : -1
                );
            }
            return sum.Mean(1);
        }
    }
}

internal sealed class MassNormalizedStage(double alpha)
{
    private readonly ExactMeanAccumulator _retention = Retention(alpha);
    private double _mean,
        _mass,
        _residual = 1;

    internal void Reset()
    {
        _mean = _mass = 0;
        _residual = 1;
    }

    internal double Next(double input)
    {
        if (_residual == 0)
        {
            // A previous update has already reset mass to one. Factor the exact
            // convex sum; keep the first zero-residual update on the path below
            // because it must still use the previous, nonunit startup mass.
            var previous = JurikDyadic.FromDouble(_mean);
            var next = JurikDyadic.FromDouble(input);
            if (JurikDyadic.TryBlend(previous, next, alpha, out var blended))
            {
                var value = blended.Mean(1);
                // The reusable blend canonicalizes zero. This stage publishes
                // directly, so use exact arithmetic to retain negative underflow.
                if (value != 0) // NOSONAR: Preserve the signed-zero rounding contract.
                {
                    _mean = value;
                    return _mean;
                }
            }
            next.Subtract(previous);
            next.Multiply(alpha);
            next.AddExact(previous);
            _mean = next.Mean(1);
            return _mean;
        }
        _residual = _residual > 1e-10 ? (1 - alpha) * _residual : 0;
        if (_mass == 0) // NOSONAR: Only the initial/reset state has zero mass.
        {
            // The exact numerator alpha*input divided by alpha is the input,
            // including tiny alphas whose floating product would underflow.
            _mean = input == 0 ? 0 : input; // NOSONAR: Preserve canonical exact zero.
            _mass = alpha;
            return _mean;
        }
        // Retain exact weights on their natural binary scales instead of padding
        // every term to the minimum-subnormal grid. No intermediate is rounded.
        var previousWeight = _retention;
        previousWeight.Multiply(_mass);
        var weight = previousWeight;
        weight.Add(alpha);
        var numerator = previousWeight;
        numerator.Multiply(_mean);
        numerator.AddProduct(input, alpha);
        _mean = _residual == 0 ? numerator.Mean(1) : numerator.Ratio(weight);
        _mass = _residual == 0 ? 1 : weight.Mean(1);
        return _mean;
    }

    private static ExactMeanAccumulator Retention(double alpha)
    {
        var retention = new ExactMeanAccumulator();
        retention.Add(1);
        retention.Add(alpha, -1);
        return retention;
    }
}
