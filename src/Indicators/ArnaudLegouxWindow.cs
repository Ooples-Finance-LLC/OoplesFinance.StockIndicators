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
        if (!double.IsFinite(offset))
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (!double.IsFinite(sigma))
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

    private sealed class State(int period, double offset, double sigma, bool fullWindowOnly)
        : IMultiOutputState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private readonly Queue<double> _history = new();
        private readonly BigInteger _offset = ExactVarianceWindow.Units(offset);
        private readonly BigInteger _sigmaSquared = BigInteger.Pow(
            ExactVarianceWindow.Units(sigma),
            2
        );
        private double[] _weights = [];

        public void Reset()
        {
            _history.Clear();
            _weights = [];
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
            var divisor = new BigInteger(2L * n * n) << 4296;
            _weights = new double[n];
            for (var i = 0; i < n; i++)
            {
                // Difference of squares factored before multiplication: no rounded
                // center or overflowing squared distance can create a false tie.
                var difference = (i - peak) * Grid * ((i + (long)peak) * Grid - 2 * center);
                var numerator = difference * _sigmaSquared;
                _weights[i] =
                    numerator >= 746 * divisor
                        ? 0
                        : Math.Exp(-ExactMeanAccumulator.UnitRatio(numerator << 1074, divisor));
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (_history.Count == period)
                _history.Dequeue();
            _history.Enqueue(bar.Close);
            if (fullWindowOnly && _history.Count < period)
                return;
            PrepareWeights(_history.Count);
            var numerator = new ExactMeanAccumulator();
            var denominator = new ExactMeanAccumulator();
            var i = 0;
            foreach (var price in _history)
            {
                numerator.AddProduct(price, _weights[i]);
                denominator.Add(_weights[i++]);
            }
            output[0] = numerator.Ratio(denominator);
            output[1] = 1;
        }
    }
}
