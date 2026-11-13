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
            var first = _stages[0].Next(bar.Close);
            var second = _stages[1].Next(first);
            var third = _stages[2].Next(second);
            var fourth = _stages[3].Next(third);
#if !NETFRAMEWORK
            if (TryCombine(first, second, third, fourth, out var combined)) return combined;
#endif
            var sum = new ExactMeanAccumulator();
            sum.Add(first, 4);
            sum.Add(second, -6);
            sum.Add(third, 4);
            sum.Add(fourth, -1);
            return sum.Mean(1);
        }

#if !NETFRAMEWORK
        private static bool TryCombine(double first, double second, double third, double fourth, out double value)
        {
            if (TryCombineCommonBinade(first, second, third, fourth, out value)) return true;
            // 4*a-6*b+4*c-d = a+3*((a-b)+(c-b))+(c-d). Certify
            // every intermediate exactly; only the final addition may round.
            if (!Binary64ArithmeticCertificate.TryDifference(first, second, out var ab)
                || !Binary64ArithmeticCertificate.TryDifference(third, second, out var cb)
                || !Binary64ArithmeticCertificate.TryDifference(third, fourth, out var cd)
                || !Binary64ArithmeticCertificate.TryDifference(ab, -cb, out var curvature)) return false;
            double weighted = 0;
            if (curvature != 0 && !Binary64ArithmeticCertificate.TryProduct(curvature, 3, out weighted)) return false; // NOSONAR: Exact zero needs no product certificate.
            if (!Binary64ArithmeticCertificate.TryDifference(weighted, -cd, out var correction)) return false;
            value = first + correction;
            // Integer weights cannot underflow a nonzero binary64-grid sum.
            // Preserve the original exact accumulator's canonical zero.
            if (value == 0) value = 0; // NOSONAR: Canonicalize both exact zero signs.
            return double.IsFinite(value);
        }

        private static bool TryCombineCommonBinade(double first, double second, double third, double fourth, out double value)
        {
            value = 0;
            ulong firstBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(first));
            ulong binade = firstBits >> 52;
            int exponent = (int)(binade & 0x7ff);
            // A nonzero integer sum scaled by 2^(E-1075) is normal for E>=53.
            // Excluding smaller exponents avoids a second subnormal rounding.
            if (exponent is < 53 or > 2046) return false;
            ulong secondBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(second));
            ulong thirdBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(third));
            ulong fourthBits = unchecked((ulong)BitConverter.DoubleToInt64Bits(fourth));
            if (secondBits >> 52 != binade || thirdBits >> 52 != binade || fourthBits >> 52 != binade) return false;
            if (firstBits == secondBits && firstBits == thirdBits && firstBits == fourthBits)
            {
                value = first; // Integer coefficients sum to one.
                return true;
            }
            const ulong fractionMask = (1UL << 52) - 1;
            const long hidden = 1L << 52;
            long a = (long)(firstBits & fractionMask) | hidden;
            long b = (long)(secondBits & fractionMask) | hidden;
            long c = (long)(thirdBits & fractionMask) | hidden;
            long d = (long)(fourthBits & fractionMask) | hidden;
            // The absolute sum is bounded by 15*2^53 < 2^57, including every
            // intermediate. Only conversion to double rounds; normal scaling
            // by a power of two preserves that rounding. Overflow falls back.
            long sum = 4 * a - 6 * b + 4 * c - d;
            if ((binade & 0x800) != 0) sum = -sum;
            if (sum == 0) return true; // Exact cancellation publishes canonical +0.
            double rounded = Math.ScaleB((double)sum, exponent - 1075);
            if (!double.IsFinite(rounded)) return false;
            value = rounded;
            return true;
        }
#endif
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
