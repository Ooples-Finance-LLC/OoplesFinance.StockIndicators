using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Ooples definition: a short body strictly gapping above or below the preceding long body.</summary>
/// <remarks>Both candles must have positive ranges. A short body is at most one quarter of its range;
/// a long body is at least one half. Body endpoints must be strictly separated; shadows may overlap.
/// Returns one for either direction, otherwise zero. This is our conventional definition, not a
/// reproduction of Trady's unimplemented Stars API. All comparisons retain exact binary64 prices.</remarks>
public sealed class ConventionalStarCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    public override int WarmupBars => 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State();

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(0, ConventionalCandleReference.Stars, 0, 0),
        ];

    private sealed class State : IIndicatorState
    {
        private Bar _previous;
        private bool _long;

        public void Reset()
        {
            _previous = default;
            _long = false;
        }

        public double Update(in Bar bar)
        {
            var top = Math.Max(bar.Open, bar.Close);
            var bottom = Math.Min(bar.Open, bar.Close);
            var match =
                _long
                && bar.High > bar.Low
                && ConventionalCandleArithmetic.Compare(top, bottom, bar, 4) <= 0
                && (
                    bottom > Math.Max(_previous.Open, _previous.Close)
                    || top < Math.Min(_previous.Open, _previous.Close)
                );
            _long =
                bar.High > bar.Low
                && ConventionalCandleArithmetic.Compare(top, bottom, bar, 2) >= 0;
            _previous = bar;
            return match ? 1 : 0;
        }
    }
}

/// <summary>Ooples definition: both shadows are at most one tenth of the candle's positive high-low range.</summary>
/// <remarks>Returns one for a match, otherwise zero. Equality qualifies; zero-range candles do not.
/// This is our conventional definition, not a reproduction of Trady's unimplemented ShortShadow API.
/// All comparisons retain exact binary64 prices, including ranges larger than double.MaxValue.</remarks>
public sealed class ShortShadowsCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    protected internal override object CreateState() => new State();

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(0, ConventionalCandleReference.Shadows, 0, 0),
        ];

    private sealed class State : IIndicatorState
    {
        public void Reset() { }

        public double Update(in Bar bar) =>
            bar.High > bar.Low
            && ConventionalCandleArithmetic.Compare(
                bar.High,
                Math.Max(bar.Open, bar.Close),
                bar,
                10
            ) <= 0
            && ConventionalCandleArithmetic.Compare(Math.Min(bar.Open, bar.Close), bar.Low, bar, 10)
                <= 0
                ? 1
                : 0;
    }
}

internal static class ConventionalCandleArithmetic
{
    // Compare multiplier * segment with the full range without rounded subtraction.
    internal static int Compare(double high, double low, in Bar bar, int multiplier)
    {
        var difference = new ExactMeanAccumulator();
        difference.Add(high, multiplier);
        difference.Add(low, -multiplier);
        difference.Add(bar.High, -1);
        difference.Add(bar.Low);
        return difference.Sign;
    }
}

internal static class ConventionalCandleReference
{
    private static ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);

    internal static IReadOnlyList<double> Stars(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = 1; i < bars.Count; i++)
        {
            var a = bars[i - 1];
            var b = bars[i];
            var previousBody = (R(a.Close) - R(a.Open)).Abs();
            var body = (R(b.Close) - R(b.Open)).Abs();
            var longMinimum = (R(a.High) - R(a.Low)) / new ReferenceFraction(2);
            var shortMaximum = (R(b.High) - R(b.Low)) / new ReferenceFraction(4);
            // Check all four endpoint pairs, independent of production's extrema.
            var above =
                b.Open > a.Open && b.Open > a.Close && b.Close > a.Open && b.Close > a.Close;
            var below =
                b.Open < a.Open && b.Open < a.Close && b.Close < a.Open && b.Close < a.Close;
            result[i] =
                a.High > a.Low
                && b.High > b.Low
                && previousBody.CompareTo(longMinimum) >= 0
                && body.CompareTo(shortMaximum) <= 0
                && (above || below)
                    ? 1
                    : 0;
        }
        return result;
    }

    internal static IReadOnlyList<double> Shadows(IReadOnlyList<Bar> bars) =>
        bars.Select(b =>
            {
                var open = R(b.Open);
                var close = R(b.Close);
                var top = open.CompareTo(close) > 0 ? open : close;
                var bottom = open.CompareTo(close) < 0 ? open : close;
                var limit = (R(b.High) - R(b.Low)) / new ReferenceFraction(10);
                return
                    b.High > b.Low
                    && (R(b.High) - top).CompareTo(limit) <= 0
                    && (bottom - R(b.Low)).CompareTo(limit) <= 0
                    ? 1d
                    : 0d;
            })
            .ToArray();
}
