namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Maintains completed daily OHLC without committing preview bars.</summary>
internal sealed class DailyPivotLevels
{
    private readonly bool _standard;
    private DateTime? _day;
    private double _open, _high, _low, _close;
    private double[] _levels;
    internal static readonly string[] StandardKeys = { "Pivot", "S1", "S2", "S3", "R1", "R2", "R3", "M1", "M2", "M3", "M4", "M5", "M6" };
    internal static readonly string[] DynamicKeys = { "Pivot", "S1", "R1" };
    internal string[] Keys => _standard ? StandardKeys : DynamicKeys;

    internal DailyPivotLevels(bool standard)
    {
        _standard = standard;
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
        var pivot = _standard ? (_open + _high + _low + _close) / 4 : (_high + _low + _close) / 3;
        var s1 = 2 * pivot - _high;
        var r1 = 2 * pivot - _low;
        if (!_standard) return new[] { pivot, s1, r1 };
        var s2 = pivot - (_high - _low);
        var r2 = pivot + (_high - _low);
        // The library's Standard variant defines its third levels from R1-S1 (= high-low).
        return new[] { pivot, s1, s2, s2, r1, r2, r2, s2, (s2 + s1) / 2,
            (s1 + pivot) / 2, (r1 + pivot) / 2, (r2 + r1) / 2, r2 };
    }

    internal void Reset()
    {
        _day = null;
        _open = _high = _low = _close = 0;
        _levels = new double[Keys.Length];
    }
}
