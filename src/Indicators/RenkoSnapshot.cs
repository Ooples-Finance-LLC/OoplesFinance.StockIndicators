using System.Globalization;
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A completed Renko brick. Multiple bricks can share one source timestamp.</summary>
public sealed record RenkoBrick(
    DateTime Date,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume,
    bool IsUp
);

/// <summary>Lazy fixed-size and retrospective ATR-sized Renko charts.</summary>
public static class RenkoSnapshot
{
    /// <summary>Creates bricks from close thresholds or the larger high/low excursion (upward wins ties).</summary>
    /// <remarks>Brick size is an exact binary64 value. Initial close is rounded to one fewer decimal place than its round-trip decimal spelling, with a minimum of zero places.
    /// Accumulated volume is shared equally by a candle's bricks; each shares the accumulated high/low range.
    /// Exact arithmetic prevents overflowing counts and intermediates. Lazy enumeration does not allocate an array proportional to brick count.</remarks>
    public static IEnumerable<RenkoBrick> Calculate(
        IReadOnlyList<Bar> bars,
        double brickSize = 1,
        bool highLow = false
    )
    {
        Validate(bars);
        if (!FrameworkCompatibility.IsFinite(brickSize) || brickSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(brickSize));
        return Enumerate();
        IEnumerable<RenkoBrick> Enumerate()
        {
            if (bars.Count == 0)
                yield break;
            var text = brickSize.ToString("R", CultureInfo.InvariantCulture).Split('E', 'e');
            var dot = text[0].IndexOf('.');
            var digits = Math.Max(
                0,
                (dot < 0 ? 0 : text[0].Length - dot - 1)
                    - (text.Length == 1 ? 0 : int.Parse(text[1], CultureInfo.InvariantCulture))
                    - 1
            );
            var scale = BigInteger.Pow(10, digits);
            var grid = BigInteger.One << 1074;
            BigInteger U(double v) => ExactVarianceWindow.Units(v);
            var n = U(bars[0].Close) * scale;
            var rounded = BigInteger.DivRem(BigInteger.Abs(n), grid, out var remainder);
            if (2 * remainder > grid || 2 * remainder == grid && !rounded.IsEven)
                rounded++;
            // Remove only a proven common binary factor. The initial rounded
            // coordinate is integral; every later price can lower this shift
            // before it is consumed, preserving deferred input reads.
            int priceShift = PriceShift(brickSize);
            var close = rounded * n.Sign * (grid >> priceShift);
            // Cancel only after decimal seed rounding. Every later price and
            // step contains scale, so this factor divides all coordinates.
            var commonScale = BigInteger.GreatestCommonDivisor(close, scale);
            if (commonScale > BigInteger.One)
            {
                close /= commonScale;
                scale /= commonScale;
            }
            var open = close;
            var step = PriceUnits(brickSize, priceShift) * scale;
            double high = 0,
                low = 0;
            var volume = new ExactMeanAccumulator();
            var reset = true;
            double Price(BigInteger value)
            {
                var published = ExactMeanAccumulator.ScaledRatio(value, scale, priceShift - 1074);
                if (!FrameworkCompatibility.IsFinite(published))
                    throw new OverflowException("Renko price is not representable.");
                return published;
            }
            for (var i = 1; i < bars.Count; i++)
            {
                var bar = bars[i];
                int shift = highLow
                    ? Math.Min(PriceShift(bar.High), PriceShift(bar.Low))
                    : PriceShift(bar.Close);
                if (shift < priceShift)
                {
                    int expansion = priceShift - shift;
                    open <<= expansion;
                    close <<= expansion;
                    step <<= expansion;
                    priceShift = shift;
                }
                high = reset ? bar.High : Math.Max(high, bar.High);
                low = reset ? bar.Low : Math.Min(low, bar.Low);
                var barVolume = bar.Volume;
                // Preserve the original deferred exception before compact addition.
                if (!FrameworkCompatibility.IsFinite(barVolume)) _ = U(barVolume);
                if (reset) volume = default;
                volume.Add(barVolume);
                var upper = BigInteger.Max(open, close);
                var lower = BigInteger.Min(open, close);
                BigInteger count;
                if (highLow)
                {
                    var up = PriceUnits(bar.High, priceShift) * scale - upper;
                    var down = lower - PriceUnits(bar.Low, priceShift) * scale;
                    count = (up >= down ? up : -down) / step;
                }
                else
                {
                    var price = PriceUnits(bar.Close, priceShift) * scale;
                    count =
                        price > upper ? (price - upper) / step
                        : price < lower ? (price - lower) / step
                        : 0;
                }
                var quantity = BigInteger.Abs(count);
                reset = quantity > 0;
                if (quantity.IsZero)
                    continue;
                var divisor = new ExactMeanAccumulator();
                divisor.Add(1, quantity);
                var share = volume.Ratio(divisor);
                if (!FrameworkCompatibility.IsFinite(share))
                    throw new OverflowException("Renko volume is not representable.");
                for (BigInteger j = 0; j < quantity; j++)
                {
                    open =
                        count.Sign > 0 ? BigInteger.Max(open, close) : BigInteger.Min(open, close);
                    close = open + count.Sign * step;
                    yield return new(
                        bar.Time,
                        Price(open),
                        high,
                        low,
                        Price(close),
                        share,
                        count.Sign > 0
                    );
                }
            }
        }
    }

    private static BigInteger PriceUnits(double value, int priceShift)
    {
        // Enumeration can observe mutations after Validate. Keep the original
        // deferred exception, including its parameter name and message.
        if (!FrameworkCompatibility.IsFinite(value)) return ExactVarianceWindow.Units(value);
        if (value == 0) return BigInteger.Zero;
        var factor = new ExactMeanAccumulator.ProductFactor(value);
        // PriceShift never exceeds a finite nonzero factor's exact scale.
        return new BigInteger(factor.Mantissa) << (factor.Scale - priceShift);
    }

    // Include the significand's exact trailing zeros, keeping ordinary dyadic
    // prices in BigInteger's small representation. The integral decimal seed
    // permits at most 1074 bits of cancellation. Later finer prices still
    // expand every retained coordinate before they are consumed.
    private static int PriceShift(double value)
    {
        if (value == 0) return 1074;
        int exponent = (int)((BitConverter.DoubleToInt64Bits(value) >> 52) & 0x7ff);
        // Keep non-finite mutations deferred to the original Units call;
        // ProductFactor would otherwise throw a different exception early.
        if (exponent == 2047) return 1074;
        return Math.Min(1074, new ExactMeanAccumulator.ProductFactor(value).Scale);
    }

    /// <summary>Uses the last seeded Wilder ATR as the fixed brick size for the entire chart. Later input repaints earlier bricks.</summary>
    public static IEnumerable<RenkoBrick> CalculateAtr(
        IReadOnlyList<Bar> bars,
        int period = 14,
        bool highLow = false
    )
    {
        Validate(bars);
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
#if NETFRAMEWORK
        var window = new SeededAtrWindow(period);
        foreach (var bar in bars)
            window.Add(bar);
        if (!window.HasAverage)
            return [];
        var size = window.Average.Publish();
        if (size == 0)
            return [];
        return Calculate(bars, size, highLow);
#else
        return CalculateAtrModern(bars, period, highLow);
#endif
    }

#if !NETFRAMEWORK
    private static IEnumerable<RenkoBrick> CalculateAtrModern(IReadOnlyList<Bar> bars, int period, bool highLow)
    {
        // Bound restoration work on a certificate miss. Other periods retain
        // the original state from the first captured bar.
        SeededAtrWindow? window = period <= 64 ? null : new(period);
        bool started = false;
        double previousClose = 0, constantRange = 0;
        int seedCount = 0;
        foreach (var bar in bars)
        {
            if (window is not null)
            {
                window.Add(bar);
                continue;
            }
            // Original ATR ignores the first bar's high/low and volume.
            if (!started)
            {
                started = true;
                previousClose = bar.Close;
                continue;
            }
            if (!TryConstantAtrSpan(in bar, previousClose, seedCount, constantRange, out double span))
            {
                window = RestoreAtrConstantPrefix(period, seedCount, constantRange, previousClose);
                window.Add(bar);
                continue;
            }
            constantRange = span;
            if (seedCount < period) seedCount++;
            previousClose = bar.Close;
        }
        return CompleteAtrChart(bars, period, highLow, window, seedCount, constantRange);
    }

    private static bool TryConstantAtrSpan(in Bar bar, double previousClose, int seedCount,
        double constantRange, out double span)
    {
        span = bar.High - bar.Low;
        // Inside the closed interval, exact high-low dominates both gap terms.
        // TrueRange rounds that span once via RocBankValue.Round, giving this
        // same finite binary64 result on modern runtimes. Constant ranges seed
        // to C, and every later exact (C*(P-1)+C)/P stays C.
#pragma warning disable S1244 // Exact equality certifies an unchanged rounded true range.
        return bar.Low <= previousClose && previousClose <= bar.High
            && (seedCount == 0 ? span > 0 && FrameworkCompatibility.IsFinite(span) : span == constantRange);
#pragma warning restore S1244
    }

    private static IEnumerable<RenkoBrick> CompleteAtrChart(IReadOnlyList<Bar> bars, int period, bool highLow,
        SeededAtrWindow? window, int seedCount, double constantRange)
    {
        double size;
        if (window is null)
        {
            if (seedCount < period) return [];
            size = constantRange;
        }
        else
        {
            if (!window.HasAverage) return [];
            size = window.Average.Publish();
        }
        if (size == 0)
            return [];
        return Calculate(bars, size, highLow);
    }

    private static SeededAtrWindow RestoreAtrConstantPrefix(int period, int seedCount,
        double range, double previousClose)
    {
        var window = new SeededAtrWindow(period);
        var first = new Bar(default, 0, 0, 0, seedCount == 0 ? previousClose : 0, 0);
        window.Add(first);
        for (int i = 0; i < seedCount; i++)
        {
            // Synthetic ranges are exactly C. The last captured real close
            // becomes previousClose only after the final synthetic range.
            var bar = new Bar(default, 0, range, 0, i == seedCount - 1 ? previousClose : 0, 0);
            window.Add(bar);
        }
        // Before seeding, count and the exact C*count seed match. Once mature,
        // count is P, seed is cleared, Average/Range are C, and no other state
        // depends on prefix length. At most P+1 Add calls restore every field.
        return window;
    }
#endif

    private static void Validate(IReadOnlyList<Bar> bars)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        foreach (var b in bars)
            if (
                !FrameworkCompatibility.IsFinite(b.High)
                || !FrameworkCompatibility.IsFinite(b.Low)
                || !FrameworkCompatibility.IsFinite(b.Close)
                || !FrameworkCompatibility.IsFinite(b.Volume)
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
    }
}
