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
}
