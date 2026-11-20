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
            var exact = Exact(index);
            exact.Add(upper, -(System.Numerics.BigInteger)divisor);
            exact.Add(lower, (System.Numerics.BigInteger)divisor);
            return exact.Sign;
        }

        /// <summary>
        /// Sign of sum - divisor * ((a - b) - (c - d)), for the window of bars ending just before
        /// <paramref name="index"/>.
        /// </summary>
        internal int Compare(int index, long divisor, double a, double b, double c, double d)
        {
            var scaled = divisor * (a - b - (c - d));
            var gap = _sum - scaled;
            var margin = _slack * _largest
                + 8e-16 * divisor * (Math.Abs(a) + Math.Abs(b) + Math.Abs(c) + Math.Abs(d));
            if (gap > margin) return 1;
            if (gap < -margin) return -1;
            if (double.IsNaN(gap)) return 0;
            var exact = Exact(index);
            var weight = (System.Numerics.BigInteger)divisor;
            exact.Add(a, -weight);
            exact.Add(b, weight);
            exact.Add(c, weight);
            exact.Add(d, -weight);
            return exact.Sign;
        }

        /// <summary>
        /// Sign of numerator * sum - divisor * ((a - b) - (c - d)), for the window of bars ending just before
        /// <paramref name="index"/>.
        /// </summary>
        internal int CompareScaled(int index, int numerator, long divisor, double a, double b, double c, double d)
        {
            var scaled = divisor * (a - b - (c - d));
            var gap = numerator * _sum - scaled;
            var margin = numerator * _slack * _largest
                + 8e-16 * divisor * (Math.Abs(a) + Math.Abs(b) + Math.Abs(c) + Math.Abs(d));
            if (gap > margin) return 1;
            if (gap < -margin) return -1;
            if (double.IsNaN(gap)) return 0;
            var sum = Exact(index);
            var exact = sum;
            for (var k = 1; k < numerator; k++) exact.AddExact(sum);
            var weight = (System.Numerics.BigInteger)divisor;
            exact.Add(a, -weight);
            exact.Add(b, weight);
            exact.Add(c, weight);
            exact.Add(d, -weight);
            return exact.Sign;
        }

        // The window's sum without rounding.
        private ExactMeanAccumulator Exact(int index)
        {
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
            return exact;
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
        if (period == 0)
        {
            PlainHikkake(high, low, close, output);
            return;
        }
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


    // The plain form in two passes. Whether a bar is an inside bar and which way the next one breaks out
    // of it are as good as random, so the formation is taken as masks, a vector of bars at a time; the
    // confirmation is a short walk that only branches where a formation or a pending one exists.
    private static void PlainHikkake(double[] high, double[] low, double[] close, Span<double> output)
    {
        var n = close.Length;
        output.Slice(0, Math.Min(2, n)).Clear();
        var i = 2;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            var width = System.Numerics.Vector<double>.Count;
            var one = System.Numerics.Vector<double>.One;
            for (; i <= n - width; i += width)
            {
                var h = new System.Numerics.Vector<double>(high, i);
                var l = new System.Numerics.Vector<double>(low, i);
                var h1 = new System.Numerics.Vector<double>(high, i - 1);
                var l1 = new System.Numerics.Vector<double>(low, i - 1);
                var inside = System.Numerics.Vector.LessThan(h1, new System.Numerics.Vector<double>(high, i - 2))
                    & System.Numerics.Vector.GreaterThan(l1, new System.Numerics.Vector<double>(low, i - 2));
                var below = System.Numerics.Vector.LessThan(h, h1) & System.Numerics.Vector.LessThan(l, l1);
                var above = System.Numerics.Vector.GreaterThan(h, h1) & System.Numerics.Vector.GreaterThan(l, l1);
                System.Numerics.Vector.ConditionalSelect(inside & below, one,
                    System.Numerics.Vector.ConditionalSelect(inside & above, -one, System.Numerics.Vector<double>.Zero))
                    .CopyTo(output.Slice(i, width));
            }
        }
#endif
        for (; i < n; i++)
        {
            var inside = high[i - 1] < high[i - 2] && low[i - 1] > low[i - 2];
            output[i] = !inside ? 0
                : high[i] < high[i - 1] && low[i] < low[i - 1] ? 1
                : high[i] > high[i - 1] && low[i] > low[i - 1] ? -1
                : 0;
        }
        // Only a formation and the three bars after it can carry a signal, so the walk goes from one
        // formation to the next and leaves every other bar at the zero it already holds.
        var next = 2;
        while (next < n)
        {
            var at = next + NextNonZero(output.Slice(next));
            if (at < next) break;
            var formed = output[at];
            var confirmation = formed > 0 ? high[at - 1] : low[at - 1];
            output[at] = at >= 5 ? formed * 100 : 0;
            next = Math.Min(at + 4, n);
            for (var j = at + 1; j < next; j++)
            {
                if (output[j] != 0) // NOSONAR the formation pass stores exactly -1, 0 or 1
                {
                    // A new formation replaces the pending one.
                    next = j;
                    break;
                }
                if (formed > 0 ? close[j] > confirmation : close[j] < confirmation)
                {
                    output[j] = j >= 5 ? formed * 200 : 0;
                    next = j + 1;
                    break;
                }
            }
        }
    }

    // Offset of the first value that is not zero, or -1.
    private static int NextNonZero(ReadOnlySpan<double> values)
    {
#if NET8_0_OR_GREATER
        return values.IndexOfAnyExcept(0d);
#else
        for (var i = 0; i < values.Length; i++)
            if (values[i] != 0) return i; // NOSONAR an exact zero is the contract
        return -1;
#endif
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
        output.Slice(0, Math.Min(2, n)).Clear();
        var i = 2;
#if !NETFRAMEWORK
        if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            // Whether a candle engulfs the one before it is as good as random, so the tests are taken as
            // masks, a vector of bars at a time, instead of as branches.
            var width = System.Numerics.Vector<double>.Count;
            var bullish = new System.Numerics.Vector<double>(100);
            var bearish = new System.Numerics.Vector<double>(-100);
            for (; i <= n - width; i += width)
            {
                var o = new System.Numerics.Vector<double>(open, i);
                var c = new System.Numerics.Vector<double>(close, i);
                var po = new System.Numerics.Vector<double>(open, i - 1);
                var pc = new System.Numerics.Vector<double>(close, i - 1);
                var up = System.Numerics.Vector.GreaterThanOrEqual(c, o) & System.Numerics.Vector.LessThan(pc, po)
                    & System.Numerics.Vector.LessThanOrEqual(o, pc) & System.Numerics.Vector.GreaterThanOrEqual(c, po)
                    & (System.Numerics.Vector.LessThan(o, pc) | System.Numerics.Vector.GreaterThan(c, po));
                var down = System.Numerics.Vector.LessThan(c, o) & System.Numerics.Vector.GreaterThanOrEqual(pc, po)
                    & System.Numerics.Vector.GreaterThanOrEqual(o, pc) & System.Numerics.Vector.LessThanOrEqual(c, po)
                    & (System.Numerics.Vector.GreaterThan(o, pc) | System.Numerics.Vector.LessThan(c, po));
                System.Numerics.Vector.ConditionalSelect(up, bullish,
                    System.Numerics.Vector.ConditionalSelect(down, bearish, System.Numerics.Vector<double>.Zero))
                    .CopyTo(output.Slice(i, width));
            }
        }
#endif
        for (; i < n; i++)
        {
            double o = open[i], c = close[i], previousOpen = open[i - 1], previousClose = close[i - 1];
            if (c >= o && previousClose < previousOpen && o <= previousClose && c >= previousOpen
                && (o < previousClose || c > previousOpen))
                output[i] = 100;
            else if (c < o && previousClose >= previousOpen && o >= previousClose && c <= previousOpen
                && (o > previousClose || c < previousOpen))
                output[i] = -100;
            else
                output[i] = 0;
        }
    }


    /// <summary>
    /// Tristar, Tasuki gap and the gap three methods: a gap between two bodies and a third candle that
    /// reverses into it, with doji and equal-body tests measured against the average range.
    /// </summary>
    internal static void GapContinuationCandle(FastColumns columns, Span<double> output, int period, GapContinuationKind kind)
    {
        double[] open = columns.Open, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var tristar = kind == GapContinuationKind.Tristar;
        var divisor = (long)period * (tristar ? 10 : 5);
        // Tristar reads the average range as it stood three bars back, Tasuki two bars back.
        var ranges = new CandleWindow(columns, CandleStatistic.Range, period);
        var back = tristar ? 3 : 2;
        for (var i = 0; i < n; i++)
        {
            if (period > 0 && i >= back) ranges.Include(i - back);
            if (i < period + 2) continue;
            double topA = Math.Max(open[i - 2], close[i - 2]), bottomA = Math.Min(open[i - 2], close[i - 2]);
            double topB = Math.Max(open[i - 1], close[i - 1]), bottomB = Math.Min(open[i - 1], close[i - 1]);
            double topC = Math.Max(open[i], close[i]), bottomC = Math.Min(open[i], close[i]);
            bool upGap = bottomB > topA, downGap = topB < bottomA;
            if (tristar)
            {
                if (ranges.Compare(i - 2, divisor, topA, bottomA) < 0 || ranges.Compare(i - 2, divisor, topB, bottomB) < 0
                    || ranges.Compare(i - 2, divisor, topC, bottomC) < 0) continue;
                if (upGap && topC < topB) output[i] = -100;
                else if (downGap && bottomC > bottomB) output[i] = 100;
                continue;
            }
            var white = close[i - 1] >= open[i - 1];
            if (white == (close[i] >= open[i]) || open[i] <= bottomB || open[i] >= topB) continue;
            if (kind == GapContinuationKind.UpDownSideGapThreeMethods)
            {
                if (white == (close[i - 2] >= open[i - 2]) && (white ? upGap : downGap) && close[i] > bottomA && close[i] < topA)
                    output[i] = white ? 100 : -100;
                continue;
            }
            if (white ? !upGap || close[i] >= open[i - 1] || close[i] <= topA
                : !downGap || close[i] <= open[i - 1] || close[i] >= bottomA) continue;
            // The two bodies are equal within the average range: range sum > divisor * |bodyB - bodyC|.
            var larger = DifferenceSign(topB, bottomB, topC, bottomC) >= 0;
            var near = larger ? ranges.Compare(i - 1, divisor, topB, bottomB, topC, bottomC)
                : ranges.Compare(i - 1, divisor, topC, bottomC, topB, bottomB);
            if (near > 0) output[i] = white ? 100 : -100;
        }
    }


    /// <summary>
    /// Harami, harami cross, homing pigeon and doji star: a long body followed by a short body or a doji
    /// that sits inside it, or for the doji star gaps away from it.
    /// </summary>
    internal static void ContainmentCandle(FastColumns columns, Span<double> output, int period, ContainmentCandleKind kind)
    {
        double[] open = columns.Open, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var doji = kind is ContainmentCandleKind.HaramiCross or ContainmentCandleKind.DojiStar;
        // The long body is weighed against the bodies before it; the short one against the bars before itself.
        var longBodies = new CandleWindow(columns, CandleStatistic.Body, period);
        var recent = new CandleWindow(columns, doji ? CandleStatistic.Range : CandleStatistic.Body, period);
        var divisor = doji ? 10L * period : period;
        for (var i = 0; i < n; i++)
        {
            if (i >= 2) longBodies.Include(i - 2);
            if (i >= 1) recent.Include(i - 1);
            if (i < period + 1) continue;
            double previousOpen = open[i - 1], previousClose = close[i - 1];
            double previousTop = Math.Max(previousOpen, previousClose), previousBottom = Math.Min(previousOpen, previousClose);
            if (longBodies.Compare(i - 1, period, previousTop, previousBottom) >= 0) continue;
            double top = Math.Max(open[i], close[i]), bottom = Math.Min(open[i], close[i]);
            if (recent.Compare(i, divisor, top, bottom) < 0) continue;
            var matches = kind switch
            {
                ContainmentCandleKind.Harami => top <= previousTop && bottom >= previousBottom,
                ContainmentCandleKind.HaramiCross => top < previousTop && bottom > previousBottom,
                ContainmentCandleKind.HomingPigeon => previousClose < previousOpen && close[i] < open[i]
                    && top < previousTop && bottom > previousBottom,
                _ => previousClose >= previousOpen ? bottom > previousTop : top < previousBottom,
            };
            if (matches) output[i] = kind == ContainmentCandleKind.HomingPigeon || previousClose < previousOpen ? 100 : -100;
        }
    }


    /// <summary>
    /// Piercing line and dark cloud cover: a long body, then an opposite candle that opens beyond the
    /// first bar's extreme and closes back inside its body, past the midpoint or the given penetration.
    /// </summary>
    internal static void PenetrationCandle(FastColumns columns, Span<double> output, int period, bool piercing, double penetration)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var lagged = new CandleWindow(columns, CandleStatistic.Body, period);
        var recent = new CandleWindow(columns, CandleStatistic.Body, period);
        for (var i = 0; i < n; i++)
        {
            if (i >= 2) lagged.Include(i - 2);
            if (piercing && i >= 1) recent.Include(i - 1);
            if (i < period + 1) continue;
            double previousOpen = open[i - 1], previousClose = close[i - 1], o = open[i], c = close[i];
            if (piercing)
            {
                if (previousClose >= previousOpen || c < o || o >= low[i - 1] || c >= previousOpen) continue;
            }
            else if (previousClose < previousOpen || c >= o || o <= high[i - 1] || c <= previousOpen) continue;
            if (lagged.Compare(i - 1, period, Math.Max(previousOpen, previousClose), Math.Min(previousOpen, previousClose)) >= 0)
                continue;
            if (piercing)
            {
                if (recent.Compare(i, period, c, o) >= 0) continue;
                if (DifferenceSign(c, previousOpen, previousClose, c) > 0) output[i] = 100;
                continue;
            }
            // previousClose - close - penetration * (previousClose - previousOpen) > 0
            var gap = previousClose - c - penetration * (previousClose - previousOpen);
            var margin = 8e-16 * (Math.Abs(previousClose) + Math.Abs(c)
                + Math.Abs(penetration) * (Math.Abs(previousClose) + Math.Abs(previousOpen)));
            int sign;
            if (gap > margin) sign = 1;
            else if (gap < -margin) sign = -1;
            else
            {
                var exact = new ExactMeanAccumulator();
                exact.Add(previousClose);
                exact.Add(c, -1);
                exact.AddProduct(previousClose, penetration, -1);
                exact.AddProduct(previousOpen, penetration);
                sign = exact.Sign;
            }
            if (sign > 0) output[i] = -100;
        }
    }


    /// <summary>
    /// On-neck, in-neck and thrusting: a long black body, then a white candle that opens below its low
    /// and closes at that low, at its close, or above its close without reaching the body's midpoint.
    /// </summary>
    internal static void NeckCandle(FastColumns columns, Span<double> output, int bodyPeriod, int equalPeriod, NeckCandleKind kind)
    {
        double[] open = columns.Open, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var warmup = Math.Max(bodyPeriod, equalPeriod) + 1;
        var bodies = new CandleWindow(columns, CandleStatistic.Body, bodyPeriod);
        var ranges = new CandleWindow(columns, CandleStatistic.Range, equalPeriod);
        var divisor = 20L * equalPeriod;
        for (var i = 0; i < n; i++)
        {
            if (i >= 2)
            {
                bodies.Include(i - 2);
                ranges.Include(i - 2);
            }
            if (i < warmup) continue;
            double previousOpen = open[i - 1], previousClose = close[i - 1], c = close[i];
            if (previousClose >= previousOpen || c < open[i] || open[i] >= low[i - 1]) continue;
            if (bodies.Compare(i - 1, bodyPeriod, previousOpen, previousClose) >= 0) continue;
            var center = kind == NeckCandleKind.OnNeck ? low[i - 1] : previousClose;
            if (kind != NeckCandleKind.OnNeck && c < center) continue;
            var equal = ranges.Compare(i - 1, divisor, Math.Max(c, center), Math.Min(c, center));
            if (kind != NeckCandleKind.Thrusting)
            {
                if (equal >= 0) output[i] = -100;
                continue;
            }
            if (equal < 0 && DifferenceSign(previousOpen, c, c, previousClose) >= 0) output[i] = -100;
        }
    }


    // Sign of a - b - multiplier * (c - d), exactly.
    private static int DifferenceSign(double a, double b, double c, double d, int multiplier)
    {
        var gap = a - b - multiplier * (c - d);
        var margin = 8e-16 * (Math.Abs(a) + Math.Abs(b) + multiplier * (Math.Abs(c) + Math.Abs(d)));
        if (gap > margin) return 1;
        if (gap < -margin) return -1;
        var exact = new ExactMeanAccumulator();
        exact.Add(a);
        exact.Add(b, -1);
        exact.Add(c, -multiplier);
        exact.Add(d, multiplier);
        return exact.Sign;
    }

    /// <summary>
    /// Doji measured against the average range of the bars before it: a body no larger than a tenth of
    /// that average, alone or with the shadows that make a dragonfly, gravestone, long-legged doji or
    /// takuri line.
    /// </summary>
    internal static void AverageRangeDojiCandle(FastColumns columns, Span<double> output, int period, AverageRangeDojiKind kind)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var warmup = kind == AverageRangeDojiKind.Dragonfly ? period + 1 : period;
        var ranges = new CandleWindow(columns, CandleStatistic.Range, period);
        var tenth = 10L * period;
        for (var i = 0; i < n; i++)
        {
            if (i >= 1) ranges.Include(i - 1);
            if (i < warmup) continue;
            double top = Math.Max(open[i], close[i]), bottom = Math.Min(open[i], close[i]);
            if (ranges.Compare(i, tenth, top, bottom) < 0) continue;
            var matches = kind switch
            {
                AverageRangeDojiKind.Dragonfly => ranges.Compare(i, tenth, high[i], top) > 0
                    && ranges.Compare(i, tenth, bottom, low[i]) < 0,
                AverageRangeDojiKind.Gravestone => ranges.Compare(i, tenth, bottom, low[i]) > 0
                    && ranges.Compare(i, tenth, high[i], top) < 0,
                AverageRangeDojiKind.TakuriLine => ranges.Compare(i, tenth, high[i], top) > 0
                    && DifferenceSign(bottom, low[i], top, bottom, 2) > 0,
                AverageRangeDojiKind.LongLegged => DifferenceSign(high[i], top, top, bottom, 1) > 0
                    || DifferenceSign(bottom, low[i], top, bottom, 1) > 0,
                _ => true,
            };
            if (matches) output[i] = 100;
        }
    }


    /// <summary>
    /// Three inside, two crows, upside gap two crows and unique three river: a long first body, a second
    /// body placed against it, and a third candle that confirms or reverses.
    /// </summary>
    internal static void TripleBodyCandle(FastColumns columns, Span<double> output, int longPeriod, int shortPeriod,
        TripleBodyCandleKind kind)
    {
        double[] open = columns.Open, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var crows = kind == TripleBodyCandleKind.TwoCrows;
        var river = kind == TripleBodyCandleKind.UniqueThreeRiver;
        var warmup = Math.Max(longPeriod, crows ? longPeriod : shortPeriod) + 2;
        var longBodies = new CandleWindow(columns, CandleStatistic.Body, longPeriod);
        var shortBodies = new CandleWindow(columns, CandleStatistic.Body, shortPeriod);
        // The river weighs the newest body against the bars before it; the others weigh the middle one.
        var shortBack = river ? 1 : 2;
        for (var i = 0; i < n; i++)
        {
            if (i >= 3) longBodies.Include(i - 3);
            if (!crows && i >= shortBack) shortBodies.Include(i - shortBack);
            if (i < warmup) continue;
            double openA = open[i - 2], closeA = close[i - 2], openB = open[i - 1], closeB = close[i - 1], o = open[i], c = close[i];
            double topA = Math.Max(openA, closeA), bottomA = Math.Min(openA, closeA);
            double topB = Math.Max(openB, closeB), bottomB = Math.Min(openB, closeB);
            var matches = kind switch
            {
                TripleBodyCandleKind.ThreeInside => topB < topA && bottomB > bottomA
                    && (closeA >= openA ? c < o && c < openA : c >= o && c > openA),
                TripleBodyCandleKind.TwoCrows => closeA >= openA && closeB < openB && closeB > closeA && c < o && o < openB
                    && o > closeB && c > openA && c < closeA,
                TripleBodyCandleKind.UpsideGapTwoCrows => closeA >= openA && closeB < openB && closeB > closeA && c < o
                    && o > openB && c < closeB && c > closeA,
                _ => closeA < openA && closeB < openB && closeB > closeA && openB <= openA && low[i - 1] < low[i - 2]
                    && c >= o && o > low[i - 1],
            };
            if (!matches) continue;
            if (longBodies.Compare(i - 2, longPeriod, topA, bottomA) >= 0) continue;
            if (!crows)
            {
                var margin = river
                    ? shortBodies.Compare(i, shortPeriod, Math.Max(o, c), Math.Min(o, c))
                    : shortBodies.Compare(i - 1, shortPeriod, topB, bottomB);
                if (river ? margin <= 0 : margin < 0) continue;
            }
            output[i] = river || kind == TripleBodyCandleKind.ThreeInside && closeA < openA ? 100 : -100;
        }
    }


    /// <summary>
    /// Counterattack, separating lines and gap side-by-side white lines: two candles whose closes or
    /// opens match within a fraction of the average range.
    /// </summary>
    internal static void MatchedLinesCandle(FastColumns columns, Span<double> output, int bodyPeriod, int rangePeriod,
        int equalPeriod, MatchedLinesKind kind)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var gap = kind == MatchedLinesKind.GapSideBySideWhiteLines;
        var counter = kind == MatchedLinesKind.Counterattack;
        var warmup = Math.Max(bodyPeriod, Math.Max(rangePeriod, equalPeriod)) + (gap ? 2 : 1);
        var equals = new CandleWindow(columns, CandleStatistic.Range, equalPeriod);
        var bodies = new CandleWindow(columns, CandleStatistic.Body, bodyPeriod);
        var laggedBodies = new CandleWindow(columns, CandleStatistic.Body, bodyPeriod);
        var ranges = new CandleWindow(columns, CandleStatistic.Range, rangePeriod);
        long equalDivisor = 20L * equalPeriod, rangeDivisor = (long)rangePeriod * (gap ? 5 : 10);
        for (var i = 0; i < n; i++)
        {
            if (i >= 2)
            {
                equals.Include(i - 2);
                if (counter) laggedBodies.Include(i - 2);
                if (gap) ranges.Include(i - 2);
            }
            if (i >= 1)
            {
                if (!gap) bodies.Include(i - 1);
                if (kind == MatchedLinesKind.SeparatingLines) ranges.Include(i - 1);
            }
            if (i < warmup) continue;
            double openB = open[i - 1], closeB = close[i - 1], o = open[i], c = close[i];
            if (gap)
            {
                if (closeB < openB || c < o) continue;
                double topA = Math.Max(open[i - 2], close[i - 2]), bottomA = Math.Min(open[i - 2], close[i - 2]);
                bool up = openB > topA && o > topA, down = closeB < bottomA && c < bottomA;
                if (!up && !down) continue;
                if (equals.Compare(i - 1, equalDivisor, Math.Max(openB, o), Math.Min(openB, o)) < 0) continue;
                var near = DifferenceSign(c, o, closeB, openB) >= 0
                    ? ranges.Compare(i - 1, rangeDivisor, c, o, closeB, openB)
                    : ranges.Compare(i - 1, rangeDivisor, closeB, openB, c, o);
                if (near >= 0) output[i] = up ? 100 : -100;
                continue;
            }
            if ((closeB < openB) == (c < o)) continue;
            if (bodies.Compare(i, bodyPeriod, Math.Max(o, c), Math.Min(o, c)) >= 0) continue;
            if (counter)
            {
                if (laggedBodies.Compare(i - 1, bodyPeriod, Math.Max(openB, closeB), Math.Min(openB, closeB)) >= 0) continue;
                if (equals.Compare(i - 1, equalDivisor, Math.Max(closeB, c), Math.Min(closeB, c)) < 0) continue;
            }
            else
            {
                if (equals.Compare(i - 1, equalDivisor, Math.Max(openB, o), Math.Min(openB, o)) < 0) continue;
                var shadow = c >= o ? ranges.Compare(i, rangeDivisor, o, low[i]) : ranges.Compare(i, rangeDivisor, high[i], o);
                if (shadow <= 0) continue;
            }
            output[i] = c >= o ? 100 : -100;
        }
    }


    /// <summary>
    /// Strict harami: a body of the opposite colour held strictly inside the previous body, optionally
    /// with its shadows inside too, and optionally only after a run of bars trending into it.
    /// </summary>
    internal static void StrictHarami(FastColumns columns, Span<double> output, int direction, int period, bool shadows)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var trend = 0;
        for (var i = 1; i < n; i++)
        {
            double previousOpen = open[i - 1], previousClose = close[i - 1], o = open[i], c = close[i];
            bool priorBearish = previousClose < previousOpen, bearish = c < o;
            var match = priorBearish != bearish
                && (priorBearish ? previousOpen > c && previousClose < o : previousOpen < c && previousClose > o);
            if (match && shadows) match = high[i - 1] > high[i] && low[i - 1] < low[i];
            if (direction != 0)
            {
                if (match) match = trend >= period && (direction > 0 ? c > o : bearish);
                var transition = direction > 0 ? o < previousOpen && c < previousClose : o > previousOpen && c > previousClose;
                trend = transition ? trend == period ? period : trend + 1 : 0;
            }
            if (match) output[i] = 1;
        }
    }


    /// <summary>
    /// Three black crows, identical three crows and three white soldiers: three candles of one colour,
    /// each closing beyond the last with almost no shadow on the closing side.
    /// </summary>
    internal static void CrowSoldierCandle(FastColumns columns, Span<double> output, int shadowPeriod, int nearPeriod,
        int farPeriod, int bodyPeriod, CrowSoldierKind kind)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var white = kind == CrowSoldierKind.White;
        var black = kind == CrowSoldierKind.Black;
        var warmup = Math.Max(Math.Max(shadowPeriod, nearPeriod), Math.Max(farPeriod, bodyPeriod)) + (black ? 3 : 2);
        // One window per bar of the pattern: each candle is weighed against the bars before itself.
        var shadowA = new CandleWindow(columns, CandleStatistic.Range, shadowPeriod);
        var shadowB = new CandleWindow(columns, CandleStatistic.Range, shadowPeriod);
        var shadowC = new CandleWindow(columns, CandleStatistic.Range, shadowPeriod);
        var nearA = new CandleWindow(columns, CandleStatistic.Range, nearPeriod);
        var nearB = new CandleWindow(columns, CandleStatistic.Range, nearPeriod);
        var farA = new CandleWindow(columns, CandleStatistic.Range, farPeriod);
        var farB = new CandleWindow(columns, CandleStatistic.Range, farPeriod);
        var bodies = new CandleWindow(columns, CandleStatistic.Body, bodyPeriod);
        long shadowDivisor = 10L * shadowPeriod, nearDivisor = (long)nearPeriod * (white ? 5 : 20), farDivisor = 5L * farPeriod;
        for (var i = 0; i < n; i++)
        {
            if (i >= 3)
            {
                shadowA.Include(i - 3);
                if (!black) nearA.Include(i - 3);
                if (white) farA.Include(i - 3);
            }
            if (i >= 2)
            {
                shadowB.Include(i - 2);
                if (!black) nearB.Include(i - 2);
                if (white) farB.Include(i - 2);
            }
            if (i >= 1)
            {
                shadowC.Include(i - 1);
                if (white) bodies.Include(i - 1);
            }
            if (i < warmup) continue;
            double openA = open[i - 2], closeA = close[i - 2], openB = open[i - 1], closeB = close[i - 1], o = open[i], c = close[i];
            if (white
                ? closeA < openA || closeB < openB || c < o || closeB <= closeA || c <= closeB
                : closeA >= openA || closeB >= openB || c >= o || closeB >= closeA || c >= closeB)
                continue;
            if (black && !(close[i - 3] >= open[i - 3] && high[i - 3] > closeA && openB < openA && openB > closeA
                && o < openB && o > closeB))
                continue;
            if (white && !(openB > openA && o > openB)) continue;
            if (white
                ? shadowA.Compare(i - 2, shadowDivisor, high[i - 2], closeA) <= 0
                    || shadowB.Compare(i - 1, shadowDivisor, high[i - 1], closeB) <= 0
                    || shadowC.Compare(i, shadowDivisor, high[i], c) <= 0
                : shadowA.Compare(i - 2, shadowDivisor, closeA, low[i - 2]) <= 0
                    || shadowB.Compare(i - 1, shadowDivisor, closeB, low[i - 1]) <= 0
                    || shadowC.Compare(i, shadowDivisor, c, low[i]) <= 0)
                continue;
            if (black)
            {
                output[i] = -100;
                continue;
            }
            if (!white)
            {
                if (nearA.Compare(i - 2, nearDivisor, Math.Max(openB, closeA), Math.Min(openB, closeA)) >= 0
                    && nearB.Compare(i - 1, nearDivisor, Math.Max(o, closeB), Math.Min(o, closeB)) >= 0)
                    output[i] = -100;
                continue;
            }
            if (nearA.Compare(i - 2, nearDivisor, openB, closeA) >= 0
                && nearB.Compare(i - 1, nearDivisor, o, closeB) >= 0
                && farA.CompareScaled(i - 2, 3, farDivisor, closeA, openA, closeB, openB) > 0
                && farB.CompareScaled(i - 1, 3, farDivisor, closeB, openB, c, o) > 0
                && bodies.Compare(i, bodyPeriod, c, o) < 0)
                output[i] = 100;
        }
    }


    /// <summary>
    /// Advance block, stalled pattern and three stars in the south: three candles of one colour whose
    /// bodies and shadows show the move running out.
    /// </summary>
    internal static void ExhaustionCandle(FastColumns columns, Span<double> output, ExhaustionCandleKind kind, int longPeriod,
        int shortPeriod, int shadowPeriod, int nearPeriod, int farPeriod)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var advance = kind == ExhaustionCandleKind.AdvanceBlock;
        var south = kind == ExhaustionCandleKind.ThreeStarsInSouth;
        var warmup = Math.Max(Math.Max(longPeriod, shortPeriod), Math.Max(shadowPeriod, Math.Max(nearPeriod, farPeriod))) + 2;
        var shadowStatistic = advance ? CandleStatistic.Shadows : CandleStatistic.Range;
        // The digit is how many bars back the window ends: 0 weighs the newest candle, 2 the first.
        var long1 = new CandleWindow(columns, CandleStatistic.Body, longPeriod);
        var long2 = new CandleWindow(columns, CandleStatistic.Body, longPeriod);
        var short0 = new CandleWindow(columns, CandleStatistic.Body, shortPeriod);
        var shadow0 = new CandleWindow(columns, shadowStatistic, shadowPeriod);
        var shadow1 = new CandleWindow(columns, shadowStatistic, shadowPeriod);
        var shadow2 = new CandleWindow(columns, shadowStatistic, shadowPeriod);
        var near1 = new CandleWindow(columns, CandleStatistic.Range, nearPeriod);
        var near2 = new CandleWindow(columns, CandleStatistic.Range, nearPeriod);
        var far1 = new CandleWindow(columns, CandleStatistic.Range, farPeriod);
        var far2 = new CandleWindow(columns, CandleStatistic.Range, farPeriod);
        long shadowTenth = 10L * shadowPeriod, shadowHalf = 2L * shadowPeriod, nearFifth = 5L * nearPeriod, farFifth = 5L * farPeriod;
        for (var i = 0; i < n; i++)
        {
            if (i >= 3)
            {
                long2.Include(i - 3);
                if (advance) shadow2.Include(i - 3);
                if (!south) near2.Include(i - 3);
                if (advance) far2.Include(i - 3);
            }
            if (i >= 2)
            {
                if (kind == ExhaustionCandleKind.Stalled) long1.Include(i - 2);
                shadow1.Include(i - 2);
                if (!south) near1.Include(i - 2);
                if (advance) far1.Include(i - 2);
            }
            if (i >= 1)
            {
                if (!advance) short0.Include(i - 1);
                if (kind != ExhaustionCandleKind.Stalled) shadow0.Include(i - 1);
            }
            if (i < warmup) continue;
            double openA = open[i - 2], closeA = close[i - 2], openB = open[i - 1], closeB = close[i - 1], o = open[i], c = close[i];
            if (south)
            {
                if (closeA >= openA || closeB >= openB || c >= o) continue;
                double highA = high[i - 2], lowA = low[i - 2], lowB = low[i - 1];
                if (!(openB > closeA && openB <= highA && lowB < closeA && lowB >= lowA && low[i] > lowB && high[i] < high[i - 1]))
                    continue;
                if (long2.Compare(i - 2, longPeriod, openA, closeA) < 0
                    && DifferenceSign(closeA, lowA, openA, closeA) > 0
                    && DifferenceSign(openB, closeB, openA, closeA) < 0
                    && shadow1.Compare(i - 1, shadowTenth, closeB, lowB) < 0
                    && short0.Compare(i, shortPeriod, o, c) > 0
                    && shadow0.Compare(i, shadowTenth, c, low[i]) > 0
                    && shadow0.Compare(i, shadowTenth, high[i], o) > 0)
                    output[i] = 100;
                continue;
            }
            if (closeA < openA || closeB < openB || c < o || c <= closeB || closeB <= closeA || openB <= openA) continue;
            if (long2.Compare(i - 2, longPeriod, closeA, openA) >= 0) continue;
            if (near2.Compare(i - 2, nearFifth, openB, closeA) < 0) continue;
            if (!advance)
            {
                if (long1.Compare(i - 1, longPeriod, closeB, openB) < 0
                    && shadow1.Compare(i - 1, shadowTenth, high[i - 1], closeB) > 0
                    && short0.Compare(i, shortPeriod, c, o) > 0
                    && near1.Compare(i - 1, nearFifth, closeB, c) >= 0)
                    output[i] = -100;
                continue;
            }
            if (o <= openB || near1.Compare(i - 1, nearFifth, o, closeB) < 0
                || shadow2.Compare(i - 2, shadowHalf, high[i - 2], closeA) <= 0)
                continue;
            // All three are white, so each body is close - open.
            var shrinking = DifferenceSign(c, o, closeB, openB) < 0;
            if (far2.CompareScaled(i - 2, 3, farFifth, closeA, openA, closeB, openB) < 0
                    && near1.CompareScaled(i - 1, 1, nearFifth, c, o, closeB, openB) > 0
                || far1.CompareScaled(i - 1, 3, farFifth, closeB, openB, c, o) < 0
                || shrinking && DifferenceSign(closeB, openB, closeA, openA) < 0
                    && (shadow0.Compare(i, shadowHalf, high[i], c) < 0 || shadow1.Compare(i - 1, shadowHalf, high[i - 1], closeB) < 0)
                || shrinking && DifferenceSign(high[i], c, c, o) > 0)
                output[i] = -100;
        }
    }


    /// <summary>
    /// Kicking: two long marubozu of opposite colour with a gap between them, signed by the second
    /// candle or, by length, by whichever body is longer.
    /// </summary>
    internal static void KickingCandle(FastColumns columns, Span<double> output, int bodyPeriod, int shadowPeriod, bool byLength)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var warmup = Math.Max(bodyPeriod, shadowPeriod) + 1;
        var bodies = new CandleWindow(columns, CandleStatistic.Body, bodyPeriod);
        var priorBodies = new CandleWindow(columns, CandleStatistic.Body, bodyPeriod);
        var ranges = new CandleWindow(columns, CandleStatistic.Range, shadowPeriod);
        var priorRanges = new CandleWindow(columns, CandleStatistic.Range, shadowPeriod);
        var tenth = 10L * shadowPeriod;
        for (var i = 0; i < n; i++)
        {
            if (i >= 2)
            {
                priorBodies.Include(i - 2);
                priorRanges.Include(i - 2);
            }
            if (i >= 1)
            {
                bodies.Include(i - 1);
                ranges.Include(i - 1);
            }
            if (i < warmup) continue;
            double previousOpen = open[i - 1], previousClose = close[i - 1], o = open[i], c = close[i];
            var previousBlack = previousClose < previousOpen;
            if (previousBlack == (c < o)) continue;
            if (previousBlack ? low[i] <= high[i - 1] : high[i] >= low[i - 1]) continue;
            double previousTop = Math.Max(previousOpen, previousClose), previousBottom = Math.Min(previousOpen, previousClose);
            double top = Math.Max(o, c), bottom = Math.Min(o, c);
            if (priorBodies.Compare(i - 1, bodyPeriod, previousTop, previousBottom) >= 0
                || priorRanges.Compare(i - 1, tenth, high[i - 1], previousTop) <= 0
                || priorRanges.Compare(i - 1, tenth, previousBottom, low[i - 1]) <= 0
                || bodies.Compare(i, bodyPeriod, top, bottom) >= 0
                || ranges.Compare(i, tenth, high[i], top) <= 0
                || ranges.Compare(i, tenth, bottom, low[i]) <= 0)
                continue;
            var white = c >= o;
            if (byLength && DifferenceSign(top, bottom, previousTop, previousBottom) <= 0) white = previousClose >= previousOpen;
            output[i] = white ? 100 : -100;
        }
    }


    // Sign of x - y - factor * (top - bottom), exactly.
    private static int PenetrationSign(double x, double y, double top, double bottom, double factor)
    {
        var gap = x - y - factor * (top - bottom);
        var margin = 8e-16 * (Math.Abs(x) + Math.Abs(y) + Math.Abs(factor) * (Math.Abs(top) + Math.Abs(bottom)));
        if (gap > margin) return 1;
        if (gap < -margin) return -1;
        var exact = new ExactMeanAccumulator();
        exact.Add(x);
        exact.Add(y, -1);
        exact.AddProduct(top, factor, -1);
        exact.AddProduct(bottom, factor);
        return exact.Sign;
    }

    /// <summary>
    /// Morning and evening stars, their doji forms and the abandoned baby: a long body, a small body or
    /// doji gapped away from it, and a third candle closing back into the first by the given penetration.
    /// </summary>
    /// <param name="morning">The direction to look for, or null to take it from the first candle.</param>
    internal static void StarReversalCandle(FastColumns columns, Span<double> output, int longPeriod, int shortPeriod,
        int dojiPeriod, double penetration, bool? morning, bool fullGaps)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var warmup = Math.Max(Math.Max(longPeriod, shortPeriod), dojiPeriod) + 2;
        var doji = dojiPeriod > 0;
        var longs = new CandleWindow(columns, CandleStatistic.Body, longPeriod);
        var shorts = new CandleWindow(columns, CandleStatistic.Body, shortPeriod);
        var middles = doji
            ? new CandleWindow(columns, CandleStatistic.Range, dojiPeriod)
            : new CandleWindow(columns, CandleStatistic.Body, shortPeriod);
        var middleDivisor = doji ? 10L * dojiPeriod : shortPeriod;
        for (var i = 0; i < n; i++)
        {
            if (i >= 3) longs.Include(i - 3);
            if (i >= 2) middles.Include(i - 2);
            if (i >= 1) shorts.Include(i - 1);
            if (i < warmup) continue;
            double openA = open[i - 2], closeA = close[i - 2], openB = open[i - 1], closeB = close[i - 1], o = open[i], c = close[i];
            var rising = morning ?? closeA < openA;
            if (rising
                ? closeA >= openA || c < o || Math.Max(openB, closeB) >= closeA
                : closeA < openA || c >= o || Math.Min(openB, closeB) <= closeA)
                continue;
            if (fullGaps && (rising
                ? high[i - 1] >= low[i - 2] || high[i - 1] >= low[i]
                : low[i - 1] <= high[i - 2] || low[i - 1] <= high[i]))
                continue;
            double topA = Math.Max(openA, closeA), bottomA = Math.Min(openA, closeA);
            if (longs.Compare(i - 2, longPeriod, topA, bottomA) >= 0) continue;
            if (shorts.Compare(i, shortPeriod, Math.Max(o, c), Math.Min(o, c)) >= 0) continue;
            if (middles.Compare(i - 1, middleDivisor, Math.Max(openB, closeB), Math.Min(openB, closeB)) < 0) continue;
            var reached = rising
                ? PenetrationSign(c, closeA, topA, bottomA, penetration)
                : PenetrationSign(closeA, c, topA, bottomA, penetration);
            if (reached > 0) output[i] = rising ? 100 : -100;
        }
    }


    /// <summary>
    /// Breakaway, concealing baby swallow and ladder bottom: four or five candles running one way and a
    /// last one that turns against them.
    /// </summary>
    internal static void ExtendedReversalCandle(FastColumns columns, Span<double> output, int period, ExtendedReversalKind kind)
    {
        double[] open = columns.Open, high = columns.High, low = columns.Low, close = columns.Close;
        var n = close.Length;
        output = output.Slice(0, n);
        output.Clear();
        var swallow = kind == ExtendedReversalKind.ConcealingBabySwallow;
        var breakaway = kind == ExtendedReversalKind.Breakaway;
        var warmup = period + (swallow ? 3 : 4);
        var statistic = breakaway ? CandleStatistic.Body : CandleStatistic.Range;
        // Each window ends just before the candle it weighs: the first, second, third or fourth of the pattern.
        var first = new CandleWindow(columns, statistic, period);
        var second = new CandleWindow(columns, statistic, period);
        var third = new CandleWindow(columns, statistic, period);
        var fourth = new CandleWindow(columns, statistic, period);
        var divisor = (long)period * (breakaway ? 1 : 10);
        for (var i = 0; i < n; i++)
        {
            if (breakaway && i >= 5) first.Include(i - 5);
            if (swallow && i >= 4) second.Include(i - 4);
            if (swallow && i >= 3) third.Include(i - 3);
            if (!breakaway && i >= 2) fourth.Include(i - 2);
            if (i < warmup) continue;
            double openB = open[i - 3], closeB = close[i - 3], openC = open[i - 2], closeC = close[i - 2];
            double openD = open[i - 1], closeD = close[i - 1], o = open[i], c = close[i];
            if (swallow)
            {
                if (closeB >= openB || closeC >= openC || closeD >= openD || c >= o) continue;
                if (!(openD < closeC && high[i - 1] > closeC && high[i] > high[i - 1] && low[i] < low[i - 1])) continue;
                if (second.Compare(i - 3, divisor, closeB, low[i - 3]) > 0
                    && second.Compare(i - 3, divisor, high[i - 3], openB) > 0
                    && third.Compare(i - 2, divisor, closeC, low[i - 2]) > 0
                    && third.Compare(i - 2, divisor, high[i - 2], openC) > 0
                    && fourth.Compare(i - 1, divisor, high[i - 1], openD) < 0)
                    output[i] = 100;
                continue;
            }
            double openA = open[i - 4], closeA = close[i - 4];
            if (!breakaway)
            {
                if (closeA < openA && closeB < openB && closeC < openC && closeD < openD && openA > openB && openB > openC
                    && closeA > closeB && closeB > closeC && c >= o && o > openD && c > high[i - 1]
                    && fourth.Compare(i - 1, divisor, high[i - 1], openD) < 0)
                    output[i] = 100;
                continue;
            }
            var white = closeA >= openA;
            if (white != (closeB >= openB) || white != (closeD >= openD) || white == (c >= o)) continue;
            var match = white
                ? openB > closeA && high[i - 2] > high[i - 3] && low[i - 2] > low[i - 3] && high[i - 1] > high[i - 2]
                    && low[i - 1] > low[i - 2] && c < openB && c > closeA
                : openB < closeA && high[i - 2] < high[i - 3] && low[i - 2] < low[i - 3] && high[i - 1] < high[i - 2]
                    && low[i - 1] < low[i - 2] && c > openB && c < closeA;
            if (!match) continue;
            if (first.Compare(i - 4, divisor, Math.Max(openA, closeA), Math.Min(openA, closeA)) >= 0) continue;
            output[i] = white ? -100 : 100;
        }
    }
}
