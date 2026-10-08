using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Absolute candle bodies in units of the least positive binary64 value.
// Keep the subtraction and window sum exact until the final period division.
internal sealed class UltimateVolatilityWindow
{
    private readonly int _length;
    private readonly Queue<BigInteger> _bodies = new();
    private BigInteger _sum;
    internal UltimateVolatilityWindow(int length) => _length = Math.Max(1, length);

    internal (double Value, bool Active) Next(double close, double open, bool final)
    {
        var body = BigInteger.Abs(ExactVarianceWindow.Units(close) - ExactVarianceWindow.Units(open));
        var full = _bodies.Count == _length;
        var sum = _sum + body - (full ? _bodies.Peek() : BigInteger.Zero);
        var value = ExactMeanAccumulator.UnitRatio(sum, _length);
        var active = sum >= ((BigInteger)_length << 1074);
        if (final)
        {
            if (full) _bodies.Dequeue();
            _bodies.Enqueue(body); _sum = sum;
        }
        return (value, active);
    }

    internal void Reset() { _bodies.Clear(); _sum = BigInteger.Zero; }

    internal static (double[] Values, bool[] Active) Calculate(StockData data, int length)
    {
        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        for (var i = 0; i < input.Count; i++)
        { _ = ExactVarianceWindow.Units(input[i]); _ = ExactVarianceWindow.Units(data.OpenPrices[i]); }
        var window = new UltimateVolatilityWindow(length);
        var values = new double[input.Count]; var active = new bool[input.Count];
        for (var i = 0; i < input.Count; i++)
        { var point = window.Next(input[i], data.OpenPrices[i], true); values[i] = point.Value; active[i] = point.Active; }
        return (values, active);
    }
}
