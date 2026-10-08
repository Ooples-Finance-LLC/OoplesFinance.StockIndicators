#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

// A fixed binary grid is exact for ordinary windows. An unrepresentable input
// transfers the retained ranges to the general exact state before evaluation.
internal sealed class RickshawGridState(int dojiPeriod, int nearPeriod) : IPreviewIndicatorState, IOwnedHistoryBatchState
{
    private readonly long[] _dojiHistory = new long[dojiPeriod], _nearHistory = new long[nearPeriod];
    private readonly int _warmup = Math.Max(dojiPeriod, nearPeriod);
    private readonly long _dojiDen = 10L * dojiPeriod, _nearDen = 5L * nearPeriod;
    private int _seen, _dojiCount, _nearCount, _dojiPosition, _nearPosition, _exponent;
    private Int128 _doji, _near;
    private bool _useFallback;
    private RickshawManCandle.State? _fallback;

    public void Reset()
    {
        _seen = _dojiCount = _nearCount = _dojiPosition = _nearPosition = 0;
        _doji = _near = 0; _useFallback = false;
        _fallback?.Reset();
    }
    public double Update(in Bar bar) => Update(bar, true);
    public bool TryComputeBatch(OwnedBarHistory bars, double[][] output)
    {
        var offset = 0;
        var values = output[0];
        for (var chunk = 0; chunk < bars.ChunkCount; chunk++)
        {
            var input = bars.Chunk(chunk);
            for (var i = 0; i < input.Length; i++) values[offset + i] = Update(input[i]);
            offset += input.Length;
        }
        return true;
    }
    public double Update(in Bar bar, bool commit)
    {
        if (_useFallback) return _fallback!.Update(bar, commit);
        var exponent = _seen == 0 ? InitialGrid(bar) : _exponent;
        if (!Convert(bar.Open, exponent, out var open) || !Convert(bar.High, exponent, out var high)
            || !Convert(bar.Low, exponent, out var low) || !Convert(bar.Close, exponent, out var close))
        {
            _fallback ??= new RickshawManCandle.State(dojiPeriod, nearPeriod, true);
            _fallback.Seed(_dojiHistory, _dojiCount, _dojiPosition,
                _nearHistory, _nearCount, _nearPosition, _exponent, _seen);
            var result = _fallback.Update(bar, commit);
            if (commit) _useFallback = true;
            return result;
        }
        var value = _seen >= _warmup && Matches(open, high, low, close) ? 100d : 0;
        if (!commit) return value;
        _exponent = exponent;
        var range = high - low;
        Append(_dojiHistory, ref _dojiPosition, ref _dojiCount, ref _doji, range);
        Append(_nearHistory, ref _nearPosition, ref _nearCount, ref _near, range);
        if (_seen < _warmup) _seen++;
        return value;
    }
    private bool Matches(long open, long high, long low, long close)
    {
        var top = Math.Max(open, close); var bottom = Math.Min(open, close);
        var body = top - bottom;
        return (Int128)body * _dojiDen <= _doji
            && high - top > body && bottom - low > body
            && 2 * _near + _nearDen * ((Int128)high + low - 2 * (Int128)bottom) >= 0
            && 2 * _near + _nearDen * (2 * (Int128)top - high - low) >= 0;
    }
    private static void Append(long[] history, ref int position, ref int count, ref Int128 sum, long value)
    {
        if (count == history.Length) sum -= history[position]; else count++;
        history[position] = value; sum += value;
        if (++position == history.Length) position = 0;
    }
    private static int InitialGrid(in Bar bar)
    {
        var largest = Math.Max(Math.Max(Math.Abs(bar.Open), Math.Abs(bar.Close)),
            Math.Max(Math.Abs(bar.High), Math.Abs(bar.Low)));
        var exponent = (int)((BitConverter.DoubleToInt64Bits(largest) >> 52) & 2047);
        return Math.Max(-1074, Math.Max(0, exponent - 1) - 1082);
    }
    private static bool Convert(double value, int grid, out long units)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        var magnitude = (bits & ((1L << 52) - 1)) + (exponent == 0 ? 0 : 1L << 52);
        units = 0;
        if (magnitude == 0) return true;
        var shift = Math.Max(0, exponent - 1) - 1074 - grid;
        if (shift >= 0)
        {
            if (shift > 61 || magnitude > ((1L << 62) - 1) >> shift) return false;
            magnitude <<= shift;
        }
        else
        {
            if (shift < -52 || (magnitude & ((1L << -shift) - 1)) != 0) return false;
            magnitude >>= -shift;
        }
        units = bits < 0 ? -magnitude : magnitude;
        return true;
    }
}
#endif
