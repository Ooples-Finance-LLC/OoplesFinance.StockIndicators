using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Arnaud Legoux Gaussian close average with full-window or expanding startup.</summary>
/// <remarks>For n available closes, oldest-first weights are proportional to
/// exp(-(i-offset*(n-1))^2*sigma^2/(2*n^2)). Exact squared-distance differences
/// shift the largest exponent to zero before rounding each exponent to binary64
/// and applying exp. The weighted ratio rounds once. This avoids overflow and
/// all-weight underflow. Finite offsets outside [0,1] are supported; negative sigma
/// has the same effect as positive sigma and zero sigma gives a uniform mean.
/// History grows lazily. Absent startup values are zero with IsDefined equal to zero.</remarks>
public sealed class ArnaudLegouxWindow : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates ALMA with a positive period and finite offset and sigma.</summary>
    public ArnaudLegouxWindow(
        int period = 9,
        double offset = .85,
        double sigma = 6,
        bool fullWindowOnly = true
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!FrameworkCompatibility.IsFinite(offset))
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (!FrameworkCompatibility.IsFinite(sigma))
            throw new ArgumentOutOfRangeException(nameof(sigma));
        Period = period;
        Offset = offset;
        Sigma = sigma;
        FullWindowOnly = fullWindowOnly;
    }

    /// <summary>Maximum number of closes in the window.</summary>
    public int Period { get; }

    /// <summary>Gaussian center as a fraction of the oldest-to-newest interval.</summary>
    public double Offset { get; }

    /// <summary>Inverse width; zero produces a uniform mean.</summary>
    public double Sigma { get; }

    /// <summary>Whether values are absent until a complete window exists.</summary>
    public bool FullWindowOnly { get; }

    /// <summary>The weighted average, or zero before full-window startup completes.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the value is present, otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Offset, Sigma, FullWindowOnly);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(slot, bars => Reference(bars)[slot], 0, 0)
            );

    internal double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var output = new[] { new double[bars.Count], new double[bars.Count] };
        for (var end = 0; end < bars.Count; end++)
        {
            var n = Math.Min(Period, end + 1);
            if (FullWindowOnly && n < Period)
                continue;
            var center = ReferenceFraction.FromDouble(Offset) * new ReferenceFraction(n - 1);
            var squareDistances = Enumerable
                .Range(0, n)
                .Select(i =>
                {
                    var delta = new ReferenceFraction(i) - center;
                    return delta * delta;
                })
                .ToArray();
            var minimum = squareDistances.Min();
            var width = ReferenceFraction.FromDouble(Sigma);
            var scale = width * width / new ReferenceFraction(2L * n * n);
            var numerator = new ReferenceFraction(0);
            var denominator = new ReferenceFraction(0);
            for (var i = 0; i < n; i++)
            {
                var exponent = ((squareDistances[i] - minimum) * scale).ToDouble();
                var weight = ReferenceFraction.FromDouble(Math.Exp(-exponent));
                numerator += weight * ReferenceFraction.FromDouble(bars[end - n + 1 + i].Close);
                denominator += weight;
            }
            output[0][end] = (numerator / denominator).ToDouble();
            output[1][end] = 1;
        }
        return output;
    }

    internal sealed class State(int period, double offset, double sigma, bool fullWindowOnly)
        : IMultiOutputState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
#if NETFRAMEWORK
        private readonly Queue<double> _history = new();
#else
        private readonly Queue<(ExactMeanAccumulator.ProductFactor Factor, bool Finite)> _history = new();
#endif
        private readonly BigInteger _offset = ExactVarianceWindow.Units(offset);
#if NETFRAMEWORK
        private readonly BigInteger _sigmaSquared = BigInteger.Pow(
            ExactVarianceWindow.Units(sigma),
            2
        );
        private ExactMeanAccumulator.ProductCoefficient[] _weights = [];
        private ExactMeanAccumulator _denominator;
#else
        private readonly JurikDyadic _sigmaSquared = Square(sigma);
        private ExactMeanAccumulator.ProductFactor[] _weights = [];
        private JurikDyadic _denominator;

        private static JurikDyadic Square(double value)
        {
            var result = JurikDyadic.FromDouble(value);
            result.Square();
            return result;
        }
#endif

        public void Reset()
        {
            _history.Clear();
            _weights = [];
            _denominator = default;
        }

        private void PrepareWeights(int n)
        {
            if (_weights.Length == n)
                return;
            var center = _offset * (n - 1);
            var peak =
                center.Sign <= 0 ? 0
                : center >= (n - 1) * Grid ? n - 1
                : (int)((center + Grid / 2) / Grid);
#if NETFRAMEWORK
            var divisor = new BigInteger(2L * n * n) << 4296;
            _weights = new ExactMeanAccumulator.ProductCoefficient[n];
#else
            // The same factored squared-distance formula on an unscaled exact
            // dyadic grid avoids thousands of zero bits in each coefficient.
            var twiceCenter = JurikDyadic.FromDouble(offset);
            twiceCenter.Multiply(n - 1);
            twiceCenter.Multiply(2);
            var divisor = Square(n);
            divisor.Multiply(2);
            _weights = new ExactMeanAccumulator.ProductFactor[n];
#endif
            _denominator = default;
            for (var i = 0; i < n; i++)
            {
#if NETFRAMEWORK
                // Difference of squares factored before multiplication: no rounded
                // center or overflowing squared distance can create a false tie.
                var difference = (i - peak) * Grid * ((i + (long)peak) * Grid - 2 * center);
                var numerator = difference * _sigmaSquared;
                var weight =
                    numerator >= 746 * divisor
                        ? 0
                        : Math.Exp(-ExactMeanAccumulator.UnitRatio(numerator << 1074, divisor));
                _weights[i] = new ExactMeanAccumulator.ProductCoefficient(weight);
                _denominator.AddFactor(_weights[i].Factor);
#else
                var numerator = JurikDyadic.FromDouble(i + (long)peak);
                numerator.Subtract(twiceCenter);
                numerator.Multiply(i - peak);
                numerator.Multiply(_sigmaSquared);
                var weight = Math.Exp(-numerator.Ratio(divisor));
                var factor = new ExactMeanAccumulator.ProductFactor(weight);
                _weights[i] = factor;
                _denominator.AddExact(JurikDyadic.FromFactor(in factor));
#endif
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (_history.Count == period)
                _history.Dequeue();
#if NETFRAMEWORK
            _history.Enqueue(bar.Close);
#else
            // Decode each retained price once instead of once per window term.
            // Invalid state inputs must still wait until a published window
            // reaches them, matching the original full-window startup behavior.
            bool finite = double.IsFinite(bar.Close);
            _history.Enqueue((finite ? new ExactMeanAccumulator.ProductFactor(bar.Close) : default, finite));
#endif
            if (fullWindowOnly && _history.Count < period)
                return;
            PrepareWeights(_history.Count);
#if NETFRAMEWORK
            var numerator = new ExactMeanAccumulator();
#else
            var numerator = new JurikDyadic();
#endif
            var i = 0;
            foreach (var price in _history)
            {
#if NETFRAMEWORK
                var factor = new ExactMeanAccumulator.ProductFactor(price);
                numerator.AddProduct(in factor, in _weights[i++]);
#else
                if (!price.Finite) throw new ArithmeticException("An exact finite product requires finite inputs.");
                var factor = price.Factor;
                // Both normalized mantissas have at most 53 bits. Their exact
                // product fits Int128 and needs no general dyadic multiplication.
                var weight = _weights[i++];
                var product = JurikDyadic.FromBinaryProduct(
                    (Int128)factor.Mantissa * weight.Mantissa,
                    factor.Scale + weight.Scale - 2148);
                numerator.AddExact(product);
#endif
            }
            output[0] = numerator.Ratio(_denominator);
            output[1] = 1;
        }
    }
}
