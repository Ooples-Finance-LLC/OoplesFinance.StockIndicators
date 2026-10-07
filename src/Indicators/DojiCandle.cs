using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns one when the absolute candle body is strictly less than the specified fraction of its high-low range.</summary>
/// <remarks>
/// The default fraction is exactly one tenth. Equality and zero-range candles return zero.
/// The threshold is decimal; prices retain their exact binary64 values during the comparison.
/// </remarks>
public sealed class DojiCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a doji recognizer with a body fraction between zero and one, inclusive.</summary>
    public DojiCandle(decimal bodyFraction = 0.1m)
    {
        if (bodyFraction < 0 || bodyFraction > 1) throw new ArgumentOutOfRangeException(nameof(bodyFraction));
        BodyFraction = bodyFraction;
    }

    /// <summary>Exact decimal fraction used for the strict body/range comparison.</summary>
    public decimal BodyFraction { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(BodyFraction);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, Reference, 0, 0)
    ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        // Independent rational reference, parsing the decimal fraction instead of
        // production's word decomposition and signed exact accumulation.
        var text = BodyFraction.ToString(CultureInfo.InvariantCulture);
        var point = text.IndexOf('.');
        var scale = point < 0 ? 0 : text.Length - point - 1;
        var threshold = new ReferenceFraction(BigInteger.Parse(text.Replace(".", ""), CultureInfo.InvariantCulture)) /
            new ReferenceFraction(BigInteger.Pow(10, scale));
        return bars.Select(bar =>
        {
            var body = (ReferenceFraction.FromDouble(bar.Close) - ReferenceFraction.FromDouble(bar.Open)).Abs();
            var range = ReferenceFraction.FromDouble(bar.High) - ReferenceFraction.FromDouble(bar.Low);
            return body.CompareTo(range * threshold) < 0 ? 1d : 0d;
        }).ToArray();
    }

    private sealed class State : IIndicatorState
    {
        private readonly BigInteger _numerator;
        private readonly BigInteger _denominator;

        internal State(decimal fraction)
        {
            var bits = decimal.GetBits(fraction);
            _numerator = new BigInteger(unchecked((uint)bits[0])) +
                (new BigInteger(unchecked((uint)bits[1])) << 32) +
                (new BigInteger(unchecked((uint)bits[2])) << 64);
            _denominator = BigInteger.Pow(10, (bits[3] >> 16) & 255);
        }

        public void Reset() { }

        public double Update(in Bar bar)
        {
            var comparison = new ExactMeanAccumulator();
            comparison.Add(bar.High, _numerator);
            comparison.Add(bar.Low, -_numerator);
            comparison.Add(Math.Max(bar.Open, bar.Close), -_denominator);
            comparison.Add(Math.Min(bar.Open, bar.Close), _denominator);
            return comparison.Sign > 0 ? 1 : 0;
        }
    }
}
