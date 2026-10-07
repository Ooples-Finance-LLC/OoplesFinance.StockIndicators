using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A doji whose body midpoint lies strictly within the specified fraction of the candle's high.</summary>
/// <remarks>Both body and midpoint thresholds are exact decimal fractions of the full range. Equality does not qualify.</remarks>
public sealed class DragonflyDojiCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with body and midpoint fractions between zero and one, inclusive.</summary>
    public DragonflyDojiCandle(decimal bodyFraction = 0.1m, decimal shadowFraction = 0.1m)
    {
        if (shadowFraction < 0 || shadowFraction > 1) throw new ArgumentOutOfRangeException(nameof(shadowFraction));
        Uses(new DojiCandle(bodyFraction));
        BodyFraction = bodyFraction;
        ShadowFraction = shadowFraction;
    }
    /// <summary>Strict maximum body fraction of the full range.</summary>
    public decimal BodyFraction { get; }
    /// <summary>Strict maximum midpoint distance from the high, as a fraction of the full range.</summary>
    public decimal ShadowFraction { get; }
    /// <inheritdoc/>
    protected internal override object CreateState() => new ShadowDojiState(ShadowFraction, true);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => ShadowDojiReference.Evaluate(bars, BodyFraction, ShadowFraction, true), 0, 0)
    ];
}

/// <summary>A doji whose body midpoint lies strictly within the specified fraction of the candle's low.</summary>
/// <remarks>Both body and midpoint thresholds are exact decimal fractions of the full range. Equality does not qualify.</remarks>
public sealed class GravestoneDojiCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with body and midpoint fractions between zero and one, inclusive.</summary>
    public GravestoneDojiCandle(decimal bodyFraction = 0.1m, decimal shadowFraction = 0.1m)
    {
        if (shadowFraction < 0 || shadowFraction > 1) throw new ArgumentOutOfRangeException(nameof(shadowFraction));
        Uses(new DojiCandle(bodyFraction));
        BodyFraction = bodyFraction;
        ShadowFraction = shadowFraction;
    }
    /// <summary>Strict maximum body fraction of the full range.</summary>
    public decimal BodyFraction { get; }
    /// <summary>Strict maximum midpoint distance from the low, as a fraction of the full range.</summary>
    public decimal ShadowFraction { get; }
    /// <inheritdoc/>
    protected internal override object CreateState() => new ShadowDojiState(ShadowFraction, false);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, 0, 1),
        IndicatorValidationRule.Reference(0, bars => ShadowDojiReference.Evaluate(bars, BodyFraction, ShadowFraction, false), 0, 0)
    ];
}

internal sealed class ShadowDojiState : IComposedIndicatorState
{
    private readonly BigInteger _twiceNumerator;
    private readonly BigInteger _denominator;
    private readonly bool _upper;
    internal ShadowDojiState(decimal fraction, bool upper)
    {
        var bits = decimal.GetBits(fraction);
        _twiceNumerator = 2 * (new BigInteger(unchecked((uint)bits[0])) +
            (new BigInteger(unchecked((uint)bits[1])) << 32) + (new BigInteger(unchecked((uint)bits[2])) << 64));
        _denominator = BigInteger.Pow(10, (bits[3] >> 16) & 255);
        _upper = upper;
    }
    public void Reset() { }
    public double Update(in Bar bar, ReadOnlySpan<double> components)
    {
        if (components[0] <= 0) return 0;
        var comparison = new ExactMeanAccumulator();
        comparison.Add(bar.High, _twiceNumerator);
        comparison.Add(bar.Low, -_twiceNumerator);
        var bodyWeight = _upper ? _denominator : -_denominator;
        comparison.Add(bar.Open, bodyWeight);
        comparison.Add(bar.Close, bodyWeight);
        comparison.Add(_upper ? bar.High : bar.Low, -2 * bodyWeight);
        return comparison.Sign > 0 ? 1 : 0;
    }
}

internal static class ShadowDojiReference
{
    internal static IReadOnlyList<double> Evaluate(IReadOnlyList<Bar> bars, decimal bodyFraction, decimal shadowFraction, bool upper)
    {
        var bodyLimit = Fraction(bodyFraction);
        var shadowLimit = Fraction(shadowFraction);
        return bars.Select(bar =>
        {
            var open = ReferenceFraction.FromDouble(bar.Open);
            var close = ReferenceFraction.FromDouble(bar.Close);
            var high = ReferenceFraction.FromDouble(bar.High);
            var low = ReferenceFraction.FromDouble(bar.Low);
            var range = high - low;
            var midpoint = (open + close) / new ReferenceFraction(2);
            var distance = upper ? high - midpoint : midpoint - low;
            return (close - open).Abs().CompareTo(range * bodyLimit) < 0 && distance.CompareTo(range * shadowLimit) < 0 ? 1d : 0d;
        }).ToArray();
    }
    private static ReferenceFraction Fraction(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        var point = text.IndexOf('.');
        var scale = point < 0 ? 0 : text.Length - point - 1;
        return new ReferenceFraction(BigInteger.Parse(text.Replace(".", ""), CultureInfo.InvariantCulture)) /
            new ReferenceFraction(BigInteger.Pow(10, scale));
    }
}
