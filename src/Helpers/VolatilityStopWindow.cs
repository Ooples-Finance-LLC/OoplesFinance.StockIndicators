using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VolatilityStopWindow : IDisposable
{
    private readonly double _factor;
    private readonly RocBankAverage _range;
    private RocBankValue _stop;
    private ExactMeanAccumulator _difference;
    private double _previous;
    private bool _seeded, _up = true;
    internal VolatilityStopWindow(int length, double factor)
    {
        if (double.IsNaN(factor) || double.IsInfinity(factor)) throw new ArgumentOutOfRangeException(nameof(factor));
        _factor = factor; _range = new(MovingAvgType.WildersSmoothingMethod, Math.Max(1, length), 1);
    }
    internal (double Value, Signal Signal) Next(double high, double low, double close, bool commit)
    {
        var atr = _range.Next(TrueRange(high, low, _seeded ? _previous : close), commit); var stop = new RocBankValue(close); var up = _up;
        if (_seeded)
        {
            var crossing = new ExactMeanAccumulator(); crossing.Add(close); _stop.AddTo(ref crossing, -1);
            var flipped = up ? crossing.Sign < 0 : crossing.Sign > 0; if (flipped) up = !up;
            var sum = new ExactMeanAccumulator(); sum.AddProduct(atr.Mantissa, _factor, up ? -1 : 1); sum.ScaleByPowerOfTwo(atr.UpperShift); sum.Add(close); var candidate = RocBankValue.Round(sum);
            var movement = new ExactMeanAccumulator(); candidate.AddTo(ref movement); _stop.AddTo(ref movement, -1);
            stop = flipped || (up ? movement.Sign > 0 : movement.Sign < 0) ? candidate : _stop;
        }
        var difference = new ExactMeanAccumulator(); difference.Add(close); stop.AddTo(ref difference, -1); var change = difference; change.Subtract(_difference);
        var signal = difference.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : difference.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : difference.Sign > 0 ? Signal.Buy : difference.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { _stop = stop; _up = up; _previous = close; _seeded = true; _difference = difference; }
        return (stop.Publish(), signal);
    }
    internal void Reset() { _range.Reset(); _stop = default; _difference = default; _previous = 0; _seeded = false; _up = true; }
    public void Dispose() => _range.Dispose();
    private static RocBankValue TrueRange(double high, double low, double previous)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low); var p = ExactVarianceWindow.Units(previous);
        var range = BigInteger.Max(h - l, BigInteger.Max(BigInteger.Abs(h - p), BigInteger.Abs(l - p)));
        var sum = new ExactMeanAccumulator(); sum.Add(double.Epsilon, range); return RocBankValue.Round(sum);
    }
}
