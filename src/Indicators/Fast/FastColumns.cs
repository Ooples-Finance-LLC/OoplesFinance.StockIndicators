using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>Borrowed input exposed as lazily prepared, validated double columns.</summary>
/// <remarks>
/// A column is prepared once, on first use, and only the fields a kernel asks for are read or validated.
/// Caller-owned columns are used in place; a bar array has the requested field gathered out of it. The bars
/// themselves are never copied.
/// </remarks>
internal sealed class FastColumns
{
    private readonly Bar[]? _bars;
    private readonly BarColumns? _source;
    private double[]? _open, _high, _low, _close, _volume;

    internal FastColumns(Bar[] bars)
    {
        _bars = bars;
        Count = bars.Length;
    }

    internal FastColumns(BarColumns source)
    {
        _source = source;
        Count = source.Count;
    }

    // The same bars with the close column replaced, which is what reading another indicator's series means.
    private FastColumns(FastColumns bars, double[] close)
    {
        _bars = bars._bars;
        _source = bars._source;
        Count = bars.Count;
        _open = bars._open;
        _high = bars._high;
        _low = bars._low;
        _volume = bars._volume;
        _close = close;
    }

    internal int Count { get; }

    /// <summary>
    /// These bars as an indicator chained to <paramref name="series"/> reads them. A value that is not
    /// finite is put to the reader's own input rule, as the exact routes put every chained bar to it.
    /// </summary>
    internal FastColumns WithClose(double[] series, IIndicator reader)
    {
        if (!FastMath.AllFinite(series.AsSpan(0, Count)))
        {
            var domain = IndicatorInputDomain.For(reader);
            for (var i = 0; i < Count; i++)
            {
                var value = series[i];
                if (!double.IsNaN(value) && !double.IsInfinity(value)) continue;
                var bar = _bars is { } bars ? bars[i] : _source is { } source ? source[i] : default;
                domain.Validate(new Bar(bar.Time, bar.Open, bar.High, bar.Low, value, bar.Volume));
            }
        }
        return new FastColumns(this, series);
    }
    /// <summary>The bar at an index as the indicator reading these columns sees it.</summary>
    internal Bar BarAt(int index) => _bars is { } bars ? bars[index] : _source is { } source ? source[index] : default;

    internal double[] Open => _open ??= Prepare(IndicatorInputFields.Open);
    internal double[] High => _high ??= Prepare(IndicatorInputFields.High);
    internal double[] Low => _low ??= Prepare(IndicatorInputFields.Low);
    internal double[] Close => _close ??= Prepare(IndicatorInputFields.Close);
    internal double[] Volume => _volume ??= Prepare(IndicatorInputFields.Volume);

    /// <summary>
    /// Gathers the high, low and close of a bar array in one pass. Kernels that read all three call this
    /// first, so the bars are walked once instead of once per field. Borrowed columns need nothing.
    /// </summary>
    /// <summary>
    /// A column as its source holds it, not yet checked. For a kernel that proves its inputs finite in the
    /// pass that reads them, and reads the checked column, in its usual order, when that proof fails.
    /// </summary>
    internal double[] Raw(IndicatorInputFields field)
    {
        if (_source is { } source)
        {
            switch (field)
            {
                case IndicatorInputFields.Open: if (_open is null) return source.Open; break;
                case IndicatorInputFields.High: if (_high is null) return source.High; break;
                case IndicatorInputFields.Low: if (_low is null) return source.Low; break;
                case IndicatorInputFields.Close: if (_close is null) return source.Close; break;
                default: if (_volume is null) return source.Volume; break;
            }
        }
        return field switch
        {
            IndicatorInputFields.Open => Open,
            IndicatorInputFields.High => High,
            IndicatorInputFields.Low => Low,
            IndicatorInputFields.Close => Close,
            _ => Volume
        };
    }

    internal void RequireHighLowClose()
    {
        if (_bars is not { } bars || _high is not null || _low is not null || _close is not null) return;
        double[] high = FastMath.Allocate(bars.Length), low = FastMath.Allocate(bars.Length), close = FastMath.Allocate(bars.Length);
        long checkHigh = 0, checkLow = 0, checkClose = 0;
        for (var i = 0; i < bars.Length; i++)
        {
            ref readonly var bar = ref bars[i];
            double h = bar.High, l = bar.Low, c = bar.Close;
            high[i] = h;
            low[i] = l;
            close[i] = c;
            checkHigh |= NonFiniteBit(h);
            checkLow |= NonFiniteBit(l);
            checkClose |= NonFiniteBit(c);
        }
        // Reported in the order the separate gathers would have met them.
        if (checkHigh < 0) ThrowNonFinite(IndicatorInputFields.High);
        if (checkLow < 0) ThrowNonFinite(IndicatorInputFields.Low);
        if (checkClose < 0) ThrowNonFinite(IndicatorInputFields.Close);
        _high = high;
        _low = low;
        _close = close;
    }

    private double[] Prepare(IndicatorInputFields field)
    {
        if (_source is { } source)
        {
            var borrowed = field switch
            {
                IndicatorInputFields.Open => source.Open,
                IndicatorInputFields.High => source.High,
                IndicatorInputFields.Low => source.Low,
                IndicatorInputFields.Close => source.Close,
                _ => source.Volume
            };
            if (!FastMath.AllFinite(borrowed)) ThrowNonFinite(field);
            return borrowed;
        }

        var bars = _bars ?? [];
        var column = FastMath.Allocate(bars.Length);
        // One accumulated word validates the whole column without a branch per bar.
        long check = 0;
        switch (field)
        {
            case IndicatorInputFields.Open:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Open; column[i] = v; check |= NonFiniteBit(v); }
                break;
            case IndicatorInputFields.High:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].High; column[i] = v; check |= NonFiniteBit(v); }
                break;
            case IndicatorInputFields.Low:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Low; column[i] = v; check |= NonFiniteBit(v); }
                break;
            case IndicatorInputFields.Close:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Close; column[i] = v; check |= NonFiniteBit(v); }
                break;
            default:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Volume; column[i] = v; check |= NonFiniteBit(v); }
                break;
        }
        if (check < 0) ThrowNonFinite(field);
        return column;
    }

    // The sign bit is set exactly for a NaN or an infinity: the magnitude's exponent field is all ones, so
    // adding one at its lowest bit carries out of it. Gathered with an OR the test costs a cycle a bar, where
    // a running floating-point sum held every bar to the latency of an add.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long NonFiniteBit(double value) =>
        unchecked((BitConverter.DoubleToInt64Bits(value) & long.MaxValue) + (1L << 52));

    // Same exception type and message as IndicatorInputDomain.Validate, without touching unread fields.
    private static void ThrowNonFinite(IndicatorInputFields field) =>
        throw new ArgumentOutOfRangeException("bar", field + " must be finite.");
}
