using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>Borrowed bars exposed as lazily gathered, validated double columns.</summary>
/// <remarks>
/// A column is gathered once, on first use, and only the fields a kernel asks for are read or validated.
/// The bars themselves are never copied.
/// </remarks>
internal sealed class FastColumns
{
    private readonly Bar[] _bars;
    private double[]? _open, _high, _low, _close, _volume;

    internal FastColumns(Bar[] bars) => _bars = bars;

    internal int Count => _bars.Length;
    internal double[] Open => _open ??= Gather(IndicatorInputFields.Open);
    internal double[] High => _high ??= Gather(IndicatorInputFields.High);
    internal double[] Low => _low ??= Gather(IndicatorInputFields.Low);
    internal double[] Close => _close ??= Gather(IndicatorInputFields.Close);
    internal double[] Volume => _volume ??= Gather(IndicatorInputFields.Volume);

    private double[] Gather(IndicatorInputFields field)
    {
        var bars = _bars;
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
        if (check != 0 || double.IsNaN(check)) ThrowNonFinite(field);
        return column;
    }

    // Same exception type and message as IndicatorInputDomain.Validate, without touching unread fields.
    private static void ThrowNonFinite(IndicatorInputFields field) =>
        throw new ArgumentOutOfRangeException("bar", field + " must be finite.");
}
