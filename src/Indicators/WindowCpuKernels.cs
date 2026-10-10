using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed class FractalCpuKernel(int left, int right, bool useClose) : IndicatorKernel
{
    private readonly int _width = checked(left + right + 1);
    private readonly ExtremeDeque _highs = new(checked(left + right + 1), true);
    private readonly ExtremeDeque _lows = new(checked(left + right + 1), false);
    private long _seen;
    public override int OutputCount => 2;
    public override void Reset() { _seen = 0; _highs.Reset(); _lows.Reset(); }
    private protected override void Evaluate(in Bar bar, Span<double> output, bool commit)
    {
        var high = useClose ? bar.Close : bar.High;
        var low = useClose ? bar.Close : bar.Low;
        var first = _seen - _width + 1;
        output[0] = first < 0 ? double.NaN : _highs.NextUniqueAt(_seen, high, first, _seen - right) ?? double.NaN;
        output[1] = first < 0 ? double.NaN : _lows.NextUniqueAt(_seen, low, first, _seen - right) ?? double.NaN;
        if (commit)
        {
            _highs.Add(_seen, high, first); _lows.Add(_seen, low, first); _seen++;
        }
    }
}

internal sealed class PivotCpuKernel(int period, int offset, PivotLevelStyle style, bool rejectOverflow = true) : IndicatorKernel
{
    private readonly Bar[] _history = new Bar[checked(period + offset)];
    private readonly ExtremeDeque _highs = new(period, true), _lows = new(period, false);
    private long _seen;
    public override int OutputCount => 9;
    public override void Reset() { _seen = 0; _highs.Reset(); _lows.Reset(); }
    private protected override void Evaluate(in Bar bar, Span<double> output, bool commit)
    {
        var end = _seen - offset - 1;
        var previous = end >= 0 ? _history[(int)(end % _history.Length)] : default;
        var first = end - period + 1;
        if (_seen < _history.Length) output.Fill(double.NaN);
        else PivotLevelSnapshots.FillLevels(bar.Open,
            _highs.NextValue(previous.High, first), _lows.NextValue(previous.Low, first),
            previous.Close, style, output, rejectOverflow);
        if (commit)
        {
            if (end >= 0)
            {
                _highs.Add(end, previous.High, first); _lows.Add(end, previous.Low, first);
            }
            _history[(int)(_seen % _history.Length)] = bar;
            _seen++;
        }
    }
}
