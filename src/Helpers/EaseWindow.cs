using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EaseWindow
{
    private readonly BigInteger _divisor;
    private BigInteger _previousHigh, _previousLow;
    private bool _hasPrevious;
    internal EaseWindow(double divisor)
    {
        if (!double.IsFinite(divisor)) throw new ArgumentOutOfRangeException(nameof(divisor));
        _divisor = ExactVarianceWindow.Units(divisor);
    }
    internal RocBankValue Next(double high, double low, double volume, bool commit)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var v = ExactVarianceWindow.Units(volume);
        RocBankValue result = default;
        if (_hasPrevious && high != low && volume != 0)
        {
            var numerator = (h + l - _previousHigh - _previousLow) * (h - l) * _divisor * v.Sign;
            // The three-factor product and volume use 2^-1074 integer units.
            // Include the midpoint's factor of two without rounding its sum.
            for (var shift = 0; ; shift += 1024)
            {
                var value = ExactMeanAccumulator.UnitRatio(numerator, BigInteger.Abs(v) << (1075 + shift));
                if (!double.IsInfinity(value)) { result = new RocBankValue(value, shift); break; }
            }
        }
        if (commit) { _previousHigh = h; _previousLow = l; _hasPrevious = true; }
        return result;
    }
    internal void Reset() { _previousHigh = _previousLow = 0; _hasPrevious = false; }
}
