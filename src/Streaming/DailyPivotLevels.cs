using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Maintains completed daily OHLC without committing preview bars.</summary>
internal sealed class DailyPivotLevels
{
    private readonly bool _standard;
    private readonly bool _woodie;
    private readonly bool _fibonacci;
    private readonly bool _demark;
    private DateTime? _day;
    private double _open, _high, _low, _close;
    private double[] _levels;
    internal static readonly string[] StandardKeys = { "Pivot", "S1", "S2", "S3", "R1", "R2", "R3", "M1", "M2", "M3", "M4", "M5", "M6" };
    internal static readonly string[] DynamicKeys = { "Pivot", "S1", "R1" };
    internal string[] Keys => _woodie ? WoodiePivotMath.Keys : (_standard || _fibonacci) ? StandardKeys : DynamicKeys;

    internal DailyPivotLevels(bool standard, bool woodie = false, bool fibonacci = false, bool demark = false)
    {
        _standard = standard;
        _woodie = woodie;
        _fibonacci = fibonacci;
        _demark = demark;
        _levels = new double[Keys.Length];
    }

    internal double[] Next(DateTime time, double open, double high, double low, double close, bool isFinal)
    {
        var newDay = _day != time.Date;
        var levels = newDay && _day.HasValue ? Calculate() : _levels;
        if (isFinal)
        {
            if (newDay) { _open = open; _high = high; _low = low; }
            else { _high = Math.Max(_high, high); _low = Math.Min(_low, low); }
            _close = close;
            _day = time.Date;
            _levels = levels;
        }
        return levels;
    }

    private double[] Calculate()
    {
        if (_demark) return DemarkPivotMath.Levels(_open, _high, _low, _close);
        if (_fibonacci) return FibonacciPivotMath.Levels(_high, _low, _close);
        if (_woodie) return WoodiePivotMath.Levels(_high, _low, _close);
        return DailyPivotMath.Levels(_open, _high, _low, _close, _standard);
    }

    internal void Reset()
    {
        _day = null;
        _open = _high = _low = _close = 0;
        _levels = new double[Keys.Length];
    }
}
