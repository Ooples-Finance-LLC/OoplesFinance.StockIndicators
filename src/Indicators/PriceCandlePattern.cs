using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Price-relative doji or range-relative marubozu classification.</summary>
public enum PriceCandleKind
{
    /// <summary>Body at most the requested percentage of the absolute open; a zero open never matches.</summary>
    RelativeDoji,

    /// <summary>Body at least the requested percentage of the high-low range.</summary>
    Marubozu,
}

/// <summary>Classifies a candle and publishes its price, body, shadows, fractions, and direction.</summary>
/// <remarks>Doji defaults to 0.1 percent (allowed 0..0.5); marubozu defaults to 95 percent
/// (allowed 80..100). Percentages and prices are evaluated exactly as supplied binary64 numbers.
/// Match is 1 for doji, 100 for bullish marubozu, -100 for other marubozu, or zero.
/// All three fractions are one on a zero-range candle, which qualifies as bearish marubozu.
/// Price is zero when absent, with PriceIsDefined zero. Mathematically unrepresentable size outputs
/// are rejected by the runtime. Skender's intermediate decimal and binary rounding can differ at boundaries.</remarks>
public sealed class PriceCandlePattern : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a price-relative doji or range-relative marubozu recognizer.</summary>
    public PriceCandlePattern(
        PriceCandleKind kind = PriceCandleKind.RelativeDoji,
        double? percent = null
    )
        : base(12)
    {
        if (!Enum.IsDefined(typeof(PriceCandleKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var value = percent ?? (kind == PriceCandleKind.RelativeDoji ? .1 : 95);
        var minimum = kind == PriceCandleKind.RelativeDoji ? 0 : 80;
        var maximum = kind == PriceCandleKind.RelativeDoji ? .5 : 100;
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(nameof(percent));
        Kind = kind;
        Percent = value;
    }

    /// <summary>Classification formula.</summary>
    public PriceCandleKind Kind { get; }

    /// <summary>Exact supplied percentage, not a fraction.</summary>
    public double Percent { get; }

    /// <summary>Classification code: zero, one, positive 100, or negative 100.</summary>
    public IIndicatorOutput Match => Outputs[0];

    /// <summary>Close at a match; otherwise a zero placeholder.</summary>
    public IIndicatorOutput Price => Outputs[1];

    /// <summary>One when Price is present; otherwise zero.</summary>
    public IIndicatorOutput PriceIsDefined => Outputs[2];

    /// <summary>High minus low.</summary>
    public IIndicatorOutput Size => Outputs[3];

    /// <summary>Absolute close minus open.</summary>
    public IIndicatorOutput Body => Outputs[4];

    /// <summary>High minus the body top.</summary>
    public IIndicatorOutput UpperWick => Outputs[5];

    /// <summary>Body bottom minus low.</summary>
    public IIndicatorOutput LowerWick => Outputs[6];

    /// <summary>Body divided by range, or one for zero range.</summary>
    public IIndicatorOutput BodyFraction => Outputs[7];

    /// <summary>Upper wick divided by range, or one for zero range.</summary>
    public IIndicatorOutput UpperWickFraction => Outputs[8];

    /// <summary>Lower wick divided by range, or one for zero range.</summary>
    public IIndicatorOutput LowerWickFraction => Outputs[9];

    /// <summary>One when close is above open.</summary>
    public IIndicatorOutput IsBullish => Outputs[10];

    /// <summary>One when close is below open.</summary>
    public IIndicatorOutput IsBearish => Outputs[11];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Kind, Percent);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 12)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        bars.Select(bar => PriceCandleReference.Evaluate(bar, Kind, Percent)[slot])
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(PriceCandleKind kind, double percent) : IMultiOutputState
    {
        public void Reset() { }

        public void Update(in Bar bar, Span<double> output)
        {
            var top = Math.Max(bar.Open, bar.Close);
            var bottom = Math.Min(bar.Open, bar.Close);
            var size = Difference(bar.High, bar.Low);
            var body = Difference(top, bottom);
            var upper = Difference(bar.High, top);
            var lower = Difference(bottom, bar.Low);
            bool match;
            var threshold = new ExactMeanAccumulator();
            if (kind == PriceCandleKind.RelativeDoji)
            {
                threshold.AddProduct(Math.Abs(bar.Open), percent);
                threshold.Add(top, -100);
                threshold.Add(bottom, 100);
                match = bar.Open != 0 && threshold.Sign >= 0;
            }
            else
            {
                threshold.Add(top, 100);
                threshold.Add(bottom, -100);
                threshold.AddProduct(bar.High, percent, -1);
                threshold.AddProduct(bar.Low, percent);
                match = size.IsExactlyZero || threshold.Sign >= 0;
            }
            output[0] =
                !match ? 0
                : kind == PriceCandleKind.RelativeDoji ? 1
                : bar.Close > bar.Open ? 100
                : -100;
            output[1] = match ? bar.Close : 0;
            output[2] = match ? 1 : 0;
            output[3] = size.Mean(1);
            output[4] = body.Mean(1);
            output[5] = upper.Mean(1);
            output[6] = lower.Mean(1);
            output[7] = size.IsExactlyZero ? 1 : body.Ratio(size);
            output[8] = size.IsExactlyZero ? 1 : upper.Ratio(size);
            output[9] = size.IsExactlyZero ? 1 : lower.Ratio(size);
            output[10] = bar.Close > bar.Open ? 1 : 0;
            output[11] = bar.Close < bar.Open ? 1 : 0;
        }

        private static ExactMeanAccumulator Difference(double high, double low)
        {
            var result = new ExactMeanAccumulator();
            result.Add(high);
            result.Add(low, -1);
            return result;
        }
    }
}

internal static class PriceCandleReference
{
    internal static double[] Evaluate(in Bar bar, PriceCandleKind kind, double percent)
    {
        var open = ReferenceFraction.FromDouble(bar.Open);
        var close = ReferenceFraction.FromDouble(bar.Close);
        var high = ReferenceFraction.FromDouble(bar.High);
        var low = ReferenceFraction.FromDouble(bar.Low);
        var range = high - low;
        var body = (close - open).Abs();
        var upper = high - (open.CompareTo(close) > 0 ? open : close);
        var lower = (open.CompareTo(close) < 0 ? open : close) - low;
        var fraction = ReferenceFraction.FromDouble(percent) / new ReferenceFraction(100);
        var match =
            kind == PriceCandleKind.RelativeDoji
                ? bar.Open != 0 && (body / open.Abs()).CompareTo(fraction) <= 0
                : range.Sign == 0 || (body / range).CompareTo(fraction) >= 0;
        return
        [
            !match ? 0
            : kind == PriceCandleKind.RelativeDoji ? 1
            : bar.Close > bar.Open ? 100
            : -100,
            match ? bar.Close : 0,
            match ? 1 : 0,
            range.ToDouble(),
            body.ToDouble(),
            upper.ToDouble(),
            lower.ToDouble(),
            range.Sign == 0 ? 1 : (body / range).ToDouble(),
            range.Sign == 0 ? 1 : (upper / range).ToDouble(),
            range.Sign == 0 ? 1 : (lower / range).ToDouble(),
            bar.Close > bar.Open ? 1 : 0,
            bar.Close < bar.Open ? 1 : 0,
        ];
    }
}
