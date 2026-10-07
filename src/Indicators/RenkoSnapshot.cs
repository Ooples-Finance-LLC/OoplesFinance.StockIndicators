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
        if (!double.IsFinite(brickSize) || brickSize <= 0)
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
            var close = rounded * n.Sign * grid;
            var open = close;
            var step = U(brickSize) * scale;
            double high = 0,
                low = 0;
            BigInteger volume = 0;
            var reset = true;
            double Price(BigInteger value)
            {
                var published = ExactMeanAccumulator.UnitRatio(value, scale);
                if (!double.IsFinite(published))
                    throw new OverflowException("Renko price is not representable.");
                return published;
            }
            for (var i = 1; i < bars.Count; i++)
            {
                var bar = bars[i];
                high = reset ? bar.High : Math.Max(high, bar.High);
                low = reset ? bar.Low : Math.Min(low, bar.Low);
                volume = reset ? U(bar.Volume) : volume + U(bar.Volume);
                var upper = BigInteger.Max(open, close);
                var lower = BigInteger.Min(open, close);
                BigInteger count;
                if (highLow)
                {
                    var up = U(bar.High) * scale - upper;
                    var down = lower - U(bar.Low) * scale;
                    count = (up >= down ? up : -down) / step;
                }
                else
                {
                    var price = U(bar.Close) * scale;
                    count =
                        price > upper ? (price - upper) / step
                        : price < lower ? (price - lower) / step
                        : 0;
                }
                var quantity = BigInteger.Abs(count);
                reset = quantity > 0;
                if (quantity.IsZero)
                    continue;
                var share = ExactMeanAccumulator.UnitRatio(volume, quantity);
                if (!double.IsFinite(share))
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
        var window = new SeededAtrWindow(period);
        foreach (var bar in bars)
            window.Add(bar);
        if (!window.HasAverage)
            return [];
        var size = window.Average.Publish();
        if (size == 0)
            return [];
        return Calculate(bars, size, highLow);
    }

    private static void Validate(IReadOnlyList<Bar> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);
        foreach (var b in bars)
            if (
                !double.IsFinite(b.High)
                || !double.IsFinite(b.Low)
                || !double.IsFinite(b.Close)
                || !double.IsFinite(b.Volume)
            )
                throw new ArgumentOutOfRangeException(nameof(bars));
    }
}
