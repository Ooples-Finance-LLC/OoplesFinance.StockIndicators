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

    internal int Count { get; }
    internal double[] Open => _open ??= Prepare(IndicatorInputFields.Open);
    internal double[] High => _high ??= Prepare(IndicatorInputFields.High);
    internal double[] Low => _low ??= Prepare(IndicatorInputFields.Low);
    internal double[] Close => _close ??= Prepare(IndicatorInputFields.Close);
    internal double[] Volume => _volume ??= Prepare(IndicatorInputFields.Volume);

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
        // value - value is zero for every finite value and NaN otherwise, so one accumulated
        // sum validates the whole column without a branch per bar.
        double check = 0;
        switch (field)
        {
            case IndicatorInputFields.Open:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Open; column[i] = v; check += v - v; }
                break;
            case IndicatorInputFields.High:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].High; column[i] = v; check += v - v; }
                break;
            case IndicatorInputFields.Low:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Low; column[i] = v; check += v - v; }
                break;
            case IndicatorInputFields.Close:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Close; column[i] = v; check += v - v; }
                break;
            default:
                for (var i = 0; i < bars.Length; i++) { var v = bars[i].Volume; column[i] = v; check += v - v; }
                break;
        }
        if (!(check == 0)) ThrowNonFinite(field);
        return column;
    }

    // Same exception type and message as IndicatorInputDomain.Validate, without touching unread fields.
    private static void ThrowNonFinite(IndicatorInputFields field) =>
        throw new ArgumentOutOfRangeException("bar", field + " must be finite.");
}
