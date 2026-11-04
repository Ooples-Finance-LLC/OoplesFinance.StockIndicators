using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window midpoint SMA difference and its normalized percentage.</summary>
/// <remarks>The midpoint, each average, their difference, and 100*oscillator/midpoint
/// round separately. Both fields wait for the slow window. Normalized values are
/// absent at a zero midpoint. When chained, the upstream value replaces the midpoint. Storage grows with observed history, bounded by the
/// periods. Unrepresentable published values are rejected by the runtime.</remarks>
public sealed class AwesomeWithDetails : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates an oscillator with positive fast period below slow period.</summary>
    public AwesomeWithDetails(int fastPeriod = 5, int slowPeriod = 34)
        : base(4)
    {
        if (fastPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod <= fastPeriod)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        FastPeriod = fastPeriod;
        SlowPeriod = slowPeriod;
    }

    /// <summary>Fast mean window.</summary>
    public int FastPeriod { get; }

    /// <summary>Slow mean window and startup requirement.</summary>
    public int SlowPeriod { get; }

    /// <summary>Fast mean minus slow mean.</summary>
    public IIndicatorOutput Oscillator => Outputs[0];

    /// <summary>100 times oscillator divided by midpoint, or zero when absent.</summary>
    public IIndicatorOutput Normalized => Outputs[1];

    /// <summary>Presence of the oscillator.</summary>
    public IIndicatorOutput IsDefined => Outputs[2];

    /// <summary>Presence of the normalized percentage.</summary>
    public IIndicatorOutput IsNormalizedDefined => Outputs[3];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(FastPeriod, SlowPeriod, Source is not null);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray();
        var prices = bars.Select(b =>
                Source is not null
                    ? b.Close
                    : (
                        (ReferenceFraction.FromDouble(b.High) + ReferenceFraction.FromDouble(b.Low))
                        / new ReferenceFraction(2)
                    ).ToDouble()
            )
            .ToArray();
        for (var i = SlowPeriod - 1; i < bars.Count; i++)
        {
            double Mean(int period)
            {
                var sum = new ReferenceFraction(0);
                for (var j = i - period + 1; j <= i; j++)
                    sum += ReferenceFraction.FromDouble(prices[j]);
                return (sum / new ReferenceFraction(period)).ToDouble();
            }
            var oscillator = (
                ReferenceFraction.FromDouble(Mean(FastPeriod))
                - ReferenceFraction.FromDouble(Mean(SlowPeriod))
            ).ToDouble();
            result[0][i] = oscillator;
            result[2][i] = 1;
            if (!FrameworkCompatibility.IsFinite(oscillator) || prices[i] == 0)
                continue; // NOSONAR: Exact zero defines the ratio domain.
            result[1][i] = (
                new ReferenceFraction(100)
                * ReferenceFraction.FromDouble(oscillator)
                / ReferenceFraction.FromDouble(prices[i])
            ).ToDouble();
            result[3][i] = 1;
        }
        return result;
    }

    private sealed class MeanWindow(int period)
    {
        private readonly Queue<double> _prices = new();
        private ExactMeanAccumulator _sum;
        internal int Count => _prices.Count;

        internal void Reset()
        {
            _prices.Clear();
            _sum = default;
        }

        internal double Add(double price)
        {
            if (_prices.Count == period)
                _sum.Add(_prices.Dequeue(), -1);
            _prices.Enqueue(price);
            _sum.Add(price);
            return _sum.Mean(_prices.Count);
        }
    }

    internal sealed class State(int fast, int slow, bool chained) : IMultiOutputState
    {
        private readonly MeanWindow _fast = new(fast),
            _slow = new(slow);

        public void Reset()
        {
            _fast.Reset();
            _slow.Reset();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var price = bar.Close;
#if !NETFRAMEWORK
            // Keep malformed quote rejection even when Close supplies the input.
            if (chained ? !double.IsFinite(bar.High) || !double.IsFinite(bar.Low)
                : !Arithmetic.TryMidpoint(bar.High, bar.Low, out price))
#endif
            {
                var midpoint = new ExactMeanAccumulator();
                midpoint.Add(bar.High);
                midpoint.Add(bar.Low);
                price = chained ? bar.Close : midpoint.Mean(2);
            }
            var fastMean = _fast.Add(price);
            var slowMean = _slow.Add(price);
            if (_slow.Count < slow)
                return;
#if !NETFRAMEWORK
            var oscillator = fastMean == slowMean ? 0 : fastMean - slowMean; // NOSONAR: S1244 - Exact equality defines canonical zero.
#else
            var difference = new ExactMeanAccumulator();
            difference.Add(fastMean);
            difference.Add(slowMean, -1);
            var oscillator = difference.Mean(1);
#endif
            output[0] = oscillator;
            output[2] = 1;
            if (!FrameworkCompatibility.IsFinite(oscillator) || price == 0)
                return; // NOSONAR: Exact zero defines the ratio domain.
#if !NETFRAMEWORK
            if (Arithmetic.TryNormalize(oscillator, price, out var normalized))
                output[1] = normalized;
            else
#endif
            {
                var numerator = new ExactMeanAccumulator();
                numerator.Add(oscillator, 100);
                var denominator = new ExactMeanAccumulator();
                denominator.Add(price);
                output[1] = numerator.Ratio(denominator);
            }
            output[3] = 1;
        }
    }

#if !NETFRAMEWORK
    internal static class Arithmetic
    {
        internal static bool TryMidpoint(double high, double low, out double midpoint)
        {
            midpoint = 0;
            const double twiceMinimumNormal = 4.450147717014403e-308;
            // Guard the original inputs: a rounded, normal half could still
            // have lost a subnormal bit. Eligible halves are exact scalings.
            if (!double.IsFinite(high) || !double.IsFinite(low)
                || !(high == 0 || Math.Abs(high) >= twiceMinimumNormal)
                || !(low == 0 || Math.Abs(low) >= twiceMinimumNormal))
                return false;
            double value = high * .5 + low * .5;
            midpoint = value == 0 ? 0 : value; // NOSONAR: canonical exact zero.
            return true;
        }

        internal static bool TryNormalize(double oscillator, double price, out double normalized)
        {
            normalized = 0;
            if (!double.IsFinite(oscillator) || !double.IsFinite(price) || price == 0) return false;
            if (oscillator == 0) return true; // NOSONAR: exact zero ratio identity.
            double product = oscillator * 100, absolutePrice = Math.Abs(price);
            if (Math.Abs(product) < 3.4395525670743494e-136 || Math.Abs(product) > 2.9073548971824276e135
                || absolutePrice < 6.223015277861142e-61 || absolutePrice > 1.6069380442589903e60)
                return false;
            double candidate = product / price;
            double productError = Math.FusedMultiplyAdd(oscillator, 100, -product);
            double residual = Math.FusedMultiplyAdd(-candidate, price, product);
            // Division rounding leaves |product-candidate*price| bounded by
            // half a candidate ulp times |price|. On the exact product grid
            // that residual has <=53 bits, so its FMA is exact. Integer100's
            // product residual is exact too. The product/price guards keep
            // residuals, scaled cells and the absolute 16u bound normal.
            double error = residual + productError;
            if (price < 0) error = -error;
            double bound = (Math.Abs(residual) + Math.Abs(productError)) * 1.7763568394002505e-15; // 2^-49.
            double below = (candidate - Math.BitDecrement(candidate)) * .5 * absolutePrice;
            double above = (Math.BitIncrement(candidate) - candidate) * .5 * absolutePrice;
            if (error - bound <= -below || error + bound >= above) return false;
            normalized = candidate;
            return true;
        }
    }
#endif
}
