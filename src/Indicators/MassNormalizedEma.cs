using System.Numerics;
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
        if (!double.IsFinite(alpha) || alpha <= 0 || alpha > 1)
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
    private static readonly BigInteger Grid = BigInteger.One << 1074;
    private readonly BigInteger _alpha = ExactVarianceWindow.Units(alpha);
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
        _residual = _residual > 1e-10 ? (1 - alpha) * _residual : 0;
        var previousWeight = (Grid - _alpha) * ExactVarianceWindow.Units(_mass);
        var currentWeight = _alpha * Grid;
        var weight = previousWeight + currentWeight;
        var numerator =
            ExactVarianceWindow.Units(input) * currentWeight
            + ExactVarianceWindow.Units(_mean) * previousWeight;
        _mean = ExactMeanAccumulator.UnitRatio(numerator, _residual == 0 ? Grid * Grid : weight);
        _mass = _residual == 0 ? 1 : ExactMeanAccumulator.UnitRatio(weight, Grid);
        return _mean;
    }
}
