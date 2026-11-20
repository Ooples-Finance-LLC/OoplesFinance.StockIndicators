using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    // Which per-bar quantity a candle rule averages over the bars before the candle.
    private enum CandleStatistic
    {
        Body,
        Range,
        Shadows,
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static double CandleTerm(CandleStatistic statistic, double open, double high, double low, double close) =>
        statistic switch
        {
            CandleStatistic.Body => Math.Abs(close - open),
            CandleStatistic.Range => high - low,
            _ => high - Math.Max(open, close) + (Math.Min(open, close) - low),
        };

    /// <summary>
    /// Rolling sum of one candle statistic over the bars before the current one, with the sign of
    /// sum - divisor * (upper - lower) decided in floating point when the two sides are clearly apart and
    /// by exact arithmetic over the window when they are not. Candle rules are strict comparisons against
    /// an average, and on regular data the two sides are often exactly equal.
    /// </summary>
    private struct CandleWindow
    {
        private readonly double[] _open, _high, _low, _close;
        private readonly CandleStatistic _statistic;
        private readonly int _period;
        private readonly double _slack;
        private double _sum, _largest;

        internal CandleWindow(FastColumns columns, CandleStatistic statistic, int period)
        {
            _open = columns.Open;
            _high = columns.High;
            _low = columns.Low;
            _close = columns.Close;
            _statistic = statistic;
            _period = period;
            // Every add and subtract since the last rebuild, and every term's own rounding.
            _slack = (period + 2.0 * RebuildMask + 8) * 2.5e-16;
            _sum = _largest = 0;
        }

        /// <summary>Moves the window so that it covers the <c>period</c> bars ending at <paramref name="index"/>.</summary>
        internal void Include(int index)
        {
            if (index >= _period && (index & RebuildMask) == 0)
            {
                _sum = _largest = 0;
                for (var j = index - _period + 1; j <= index; j++)
                {
                    var term = CandleTerm(_statistic, _open[j], _high[j], _low[j], _close[j]);
                    _sum += term;
                    _largest += Math.Abs(term);
                }
                return;
            }
            var added = CandleTerm(_statistic, _open[index], _high[index], _low[index], _close[index]);
            _sum += added;
            // Everything that has passed through the sum since it was rebuilt bounds what it can have lost.
            _largest += Math.Abs(added);
            if (index < _period) return;
            var old = index - _period;
            _sum -= CandleTerm(_statistic, _open[old], _high[old], _low[old], _close[old]);
        }

        /// <summary>
        /// Sign of sum - divisor * (upper - lower), for the window of bars ending just before
        /// <paramref name="index"/>.
        /// </summary>
        internal int Compare(int index, long divisor, double upper, double lower)
        {
            var scaled = divisor * (upper - lower);
            var gap = _sum - scaled;
            var margin = _slack * _largest + 4e-16 * Math.Abs(scaled);
            if (gap > margin) return 1;
            if (gap < -margin) return -1;
            if (double.IsNaN(gap)) return 0;
            var exact = new ExactMeanAccumulator();
            for (var j = index - _period; j < index; j++)
            {
                double top = Math.Max(_open[j], _close[j]), bottom = Math.Min(_open[j], _close[j]);
                switch (_statistic)
                {
                    case CandleStatistic.Body:
                        exact.Add(top);
                        exact.Add(bottom, -1);
                        break;
                    case CandleStatistic.Range:
                        exact.Add(_high[j]);
                        exact.Add(_low[j], -1);
                        break;
                    default:
                        exact.Add(_high[j]);
                        exact.Add(top, -1);
                        exact.Add(bottom);
                        exact.Add(_low[j], -1);
                        break;
                }
            }
            exact.Add(upper, -(System.Numerics.BigInteger)divisor);
            exact.Add(lower, (System.Numerics.BigInteger)divisor);
            return exact.Sign;
        }
    }

    // Whether high - top and bottom - low both exceed multiplier * (top - bottom).
    private static bool BothShadowsExceedBody(double high, double low, double top, double bottom, int multiplier)
    {
        var body = multiplier * (top - bottom);
        var upper = high - top - body;
        var lower = bottom - low - body;
        var margin = 8e-16 * (Math.Abs(high) + Math.Abs(low) + (1 + multiplier) * (Math.Abs(top) + Math.Abs(bottom)));
        if (upper > margin && lower > margin) return true;
        if (upper < -margin || lower < -margin) return false;
        var exactUpper = new ExactMeanAccumulator();
        exactUpper.Add(high);
        exactUpper.Add(top, -1 - multiplier);
        exactUpper.Add(bottom, multiplier);
        if (exactUpper.Sign <= 0) return false;
        var exactLower = new ExactMeanAccumulator();
        exactLower.Add(bottom, 1 + multiplier);
        exactLower.Add(low, -1);
        exactLower.Add(top, -multiplier);
        return exactLower.Sign > 0;
    }

    /// <summary>
    /// Single-candle patterns that weigh the body and the shadows against their averages over the
    /// preceding bars: 100 for a rising match, -100 for a falling one, zero otherwise.
    /// </summary>
    internal static void BodyShadowCandle(FastColumns columns, Span<double> output, int period, BodyShadowCandleKind kind)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var longBody = kind is BodyShadowCandleKind.Marubozu or BodyShadowCandleKind.ClosingMarubozu
            or BodyShadowCandleKind.BeltHold or BodyShadowCandleKind.LongLine;
        var ranged = kind is BodyShadowCandleKind.Marubozu or BodyShadowCandleKind.ClosingMarubozu or BodyShadowCandleKind.BeltHold;
        var shadowed = kind is BodyShadowCandleKind.LongLine or BodyShadowCandleKind.ShortLine;
        var bodies = new CandleWindow(columns, CandleStatistic.Body, period);
        var other = new CandleWindow(columns, ranged ? CandleStatistic.Range : CandleStatistic.Shadows, period);
        long tenth = 10L * period, twice = 2L * period;
        for (var i = 0; i < n; i++)
        {
            if (i >= period)
            {
                double top = Math.Max(open[i], close[i]), bottom = Math.Min(open[i], close[i]);
                var body = bodies.Compare(i, period, top, bottom);
                if (longBody ? body < 0 : body > 0)
                {
                    var rising = close[i] >= open[i];
                    var matches = kind switch
                    {
                        BodyShadowCandleKind.Marubozu => other.Compare(i, tenth, high[i], top) > 0
                            && other.Compare(i, tenth, bottom, low[i]) > 0,
                        BodyShadowCandleKind.ClosingMarubozu => rising
                            ? other.Compare(i, tenth, high[i], top) > 0
                            : other.Compare(i, tenth, bottom, low[i]) > 0,
                        BodyShadowCandleKind.BeltHold => rising
                            ? other.Compare(i, tenth, bottom, low[i]) > 0
                            : other.Compare(i, tenth, high[i], top) > 0,
                        BodyShadowCandleKind.SpinningTop => BothShadowsExceedBody(high[i], low[i], top, bottom, 1),
                        BodyShadowCandleKind.HighWave => BothShadowsExceedBody(high[i], low[i], top, bottom, 2),
                        _ => other.Compare(i, twice, high[i], top) > 0 && other.Compare(i, twice, bottom, low[i]) > 0,
                    };
                    if (matches) output[i] = rising ? 100 : -100;
                }
            }
            bodies.Include(i);
            if (ranged || shadowed) other.Include(i);
        }
    }


    // Sign of a - b - (c - d), exactly.
    private static int DifferenceSign(double a, double b, double c, double d)
    {
        var gap = a - b - (c - d);
        var margin = 8e-16 * (Math.Abs(a) + Math.Abs(b) + Math.Abs(c) + Math.Abs(d));
        if (gap > margin) return 1;
        if (gap < -margin) return -1;
        var exact = new ExactMeanAccumulator();
        exact.Add(a);
        exact.Add(b, -1);
        exact.Add(c, -1);
        exact.Add(d);
        return exact.Sign;
    }

    /// <summary>
    /// Hammer, hanging man, inverted hammer and shooting star: a short body with one long shadow and
    /// almost none on the other side, placed against the previous bar.
    /// </summary>
    internal static void ContextReversalCandle(FastColumns columns, Span<double> output, int period, int nearPeriod,
        ContextReversalKind kind)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var warmup = Math.Max(period, nearPeriod) + 1;
        var lowerPattern = kind is ContextReversalKind.Hammer or ContextReversalKind.HangingMan;
        var value = kind is ContextReversalKind.Hammer or ContextReversalKind.InvertedHammer ? 100 : -100;
        var bodies = new CandleWindow(columns, CandleStatistic.Body, period);
        var ranges = new CandleWindow(columns, CandleStatistic.Range, period);
        var near = new CandleWindow(columns, CandleStatistic.Range, nearPeriod);
        long tenth = 10L * period, fifth = 5L * nearPeriod;
        for (var i = 0; i < n; i++)
        {
            if (i >= warmup)
            {
                double top = Math.Max(open[i], close[i]), bottom = Math.Min(open[i], close[i]);
                var matches = bodies.Compare(i, period, top, bottom) > 0
                    && (lowerPattern ? DifferenceSign(bottom, low[i], top, bottom) : DifferenceSign(high[i], top, top, bottom)) > 0
                    && ranges.Compare(i, tenth, lowerPattern ? high[i] : bottom, lowerPattern ? top : low[i]) > 0;
                if (matches)
                {
                    matches = kind switch
                    {
                        ContextReversalKind.Hammer => near.Compare(i - 1, fifth, bottom, low[i - 1]) >= 0,
                        ContextReversalKind.HangingMan => near.Compare(i - 1, fifth, high[i - 1], bottom) >= 0,
                        ContextReversalKind.InvertedHammer => top < Math.Min(open[i - 1], close[i - 1]),
                        _ => bottom > Math.Max(open[i - 1], close[i - 1]),
                    };
                    if (matches) output[i] = value;
                }
            }
            bodies.Include(i);
            ranges.Include(i);
            if (nearPeriod > 0 && i > 0) near.Include(i - 1);
        }
    }


    /// <summary>
    /// Hikkake: an inside bar followed by a bar that breaks out of it one way, signalled at 100 times the
    /// opposite direction, then 200 if a close within three bars confirms it. The modified form also
    /// needs the bar before to be an inside bar that closed near its own extreme.
    /// </summary>
    internal static void Hikkake(FastColumns columns, Span<double> output, int period)
    {
        double[] high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var ranges = new CandleWindow(columns, CandleStatistic.Range, period);
        var fifth = 5L * period;
        int direction = 0, age = 4;
        double confirmation = 0;
        for (var i = 0; i < n; i++)
        {
            // The modified form reads the range average as it stood three bars back.
            if (period > 0 && i >= 3) ranges.Include(i - 3);
            if (age < 4) age++;
            if (i < period + 2) continue;
            var signal = 0;
            var formed = 0;
            if (high[i - 1] < high[i - 2] && low[i - 1] > low[i - 2])
            {
                formed = high[i] < high[i - 1] && low[i] < low[i - 1] ? 1
                    : high[i] > high[i - 1] && low[i] > low[i - 1] ? -1
                    : 0;
                if (period != 0 && formed != 0)
                {
                    if (high[i - 2] >= high[i - 3] || low[i - 2] <= low[i - 3]) formed = 0;
                    else if (ranges.Compare(i - 2, fifth, formed > 0 ? close[i - 2] : high[i - 2],
                        formed > 0 ? low[i - 2] : close[i - 2]) < 0) formed = 0;
                }
            }
            if (formed != 0)
            {
                direction = formed;
                age = 0;
                confirmation = formed > 0 ? high[i - 1] : low[i - 1];
                signal = formed * 100;
            }
            else if (direction != 0 && age <= 3 && (direction > 0 ? close[i] > confirmation : close[i] < confirmation))
            {
                signal = direction * 200;
                direction = 0;
                age = 4;
            }
            if (i >= period + 5) output[i] = signal;
        }
    }


    /// <summary>
    /// Matching low, stick sandwich, three-line strike and three outside: two to four candles whose
    /// colours and closes line up, with "equal" and "near" measured against the average range of the bars
    /// before the candle being compared.
    /// </summary>
    internal static void SequenceCandle(FastColumns columns, Span<double> output, int period, SequenceCandleKind kind)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var warmup = kind switch
        {
            SequenceCandleKind.MatchingLow => period + 1,
            SequenceCandleKind.StickSandwich => period + 2,
            SequenceCandleKind.ThreeLineStrike => period + 3,
            _ => 3,
        };
        var divisor = (long)period * (kind == SequenceCandleKind.ThreeLineStrike ? 5 : 20);
        // The average range as it stood before the last bar, the one before it, and the one before that.
        var lag0 = new CandleWindow(columns, CandleStatistic.Range, period);
        var lag1 = new CandleWindow(columns, CandleStatistic.Range, period);
        var lag2 = new CandleWindow(columns, CandleStatistic.Range, period);
        bool use0 = kind == SequenceCandleKind.MatchingLow,
            use1 = kind is SequenceCandleKind.StickSandwich or SequenceCandleKind.ThreeLineStrike,
            use2 = kind == SequenceCandleKind.ThreeLineStrike;
        for (var i = 0; i < n; i++)
        {
            if (use0 && i >= 2) lag0.Include(i - 2);
            if (use1 && i >= 3) lag1.Include(i - 3);
            if (use2 && i >= 4) lag2.Include(i - 4);
            if (i < warmup) continue;
            var white = close[i] >= open[i];
            var lastWhite = close[i - 1] >= open[i - 1];
            switch (kind)
            {
                case SequenceCandleKind.MatchingLow:
                    if (!lastWhite && !white
                        && lag0.Compare(i - 1, divisor, Math.Max(close[i], close[i - 1]), Math.Min(close[i], close[i - 1])) >= 0)
                        output[i] = 100;
                    break;
                case SequenceCandleKind.StickSandwich:
                    if (close[i - 2] < open[i - 2] && lastWhite && !white && low[i - 1] > close[i - 2]
                        && lag1.Compare(i - 2, divisor, Math.Max(close[i], close[i - 2]), Math.Min(close[i], close[i - 2])) >= 0)
                        output[i] = 100;
                    break;
                case SequenceCandleKind.ThreeOutside:
                {
                    var middleWhite = close[i - 2] >= open[i - 2];
                    if (lastWhite && !middleWhite && close[i - 1] > open[i - 2] && open[i - 1] < close[i - 2]
                        && close[i] > close[i - 1])
                        output[i] = 100;
                    else if (!lastWhite && middleWhite && open[i - 1] > close[i - 2] && close[i - 1] < open[i - 2]
                        && close[i] < close[i - 1])
                        output[i] = -100;
                    break;
                }
                default:
                {
                    var middleWhite = close[i - 2] >= open[i - 2];
                    var firstWhite = close[i - 3] >= open[i - 3];
                    if (firstWhite != middleWhite || middleWhite != lastWhite || white == lastWhite) break;
                    // Each of the three opens inside or near the body before it.
                    double firstTop = Math.Max(open[i - 3], close[i - 3]), firstBottom = Math.Min(open[i - 3], close[i - 3]);
                    if (lag2.Compare(i - 3, divisor, open[i - 2], firstTop) < 0
                        || lag2.Compare(i - 3, divisor, firstBottom, open[i - 2]) < 0) break;
                    double middleTop = Math.Max(open[i - 2], close[i - 2]), middleBottom = Math.Min(open[i - 2], close[i - 2]);
                    if (lag1.Compare(i - 2, divisor, open[i - 1], middleTop) < 0
                        || lag1.Compare(i - 2, divisor, middleBottom, open[i - 1]) < 0) break;
                    if (lastWhite)
                    {
                        if (close[i - 1] > close[i - 2] && close[i - 2] > close[i - 3] && open[i] > close[i - 1]
                            && close[i] < open[i - 3])
                            output[i] = 100;
                    }
                    else if (close[i - 1] < close[i - 2] && close[i - 2] < close[i - 3] && open[i] < close[i - 1]
                        && close[i] > open[i - 3])
                        output[i] = -100;
                    break;
                }
            }
        }
    }


    /// <summary>
    /// Engulfing: a body of one colour that covers the previous body of the other colour and extends
    /// past at least one of its ends. Defined from the third bar.
    /// </summary>
    internal static void Engulfing(FastColumns columns, Span<double> output)
    {
        double[] open = columns.Open, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        for (var i = 2; i < n; i++)
        {
            double o = open[i], c = close[i], previousOpen = open[i - 1], previousClose = close[i - 1];
            if (c >= o && previousClose < previousOpen && o <= previousClose && c >= previousOpen
                && (o < previousClose || c > previousOpen))
                output[i] = 100;
            else if (c < o && previousClose >= previousOpen && o >= previousClose && c <= previousOpen
                && (o > previousClose || c < previousOpen))
                output[i] = -100;
        }
    }
}
