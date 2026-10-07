using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class BryantWindow
{
    private readonly int _length, _maximum; private readonly BigInteger _trend;
    private readonly Queue<(BigInteger Price, BigInteger Step)> _history = new();
    private BigInteger _travel, _previousPrice, _spread; private double _previous; private bool _started;
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private static readonly BigInteger UnitSquared = Unit * Unit;
    internal BryantWindow(int length, int maximum = 100, double trend = -1)
    {
        if (double.IsNaN(trend) || double.IsInfinity(trend)) throw new ArgumentOutOfRangeException(nameof(trend));
        _length = Math.Max(1, length); _maximum = Math.Max(1, maximum); _trend = ExactVarianceWindow.Units(trend);
    }
    private double Gain(double efficiency)
    {
        // 1 + trend*(efficiency-1/2), retained exactly until the bounded gain is rounded.
        var shape = 2 * UnitSquared + _trend * (2 * ExactVarianceWindow.Units(efficiency) - Unit);
        if (shape.IsZero) return 1;
        var numerator = shape * shape; var denominator = 2 * (_length + 1L) * UnitSquared * UnitSquared;
        if (numerator >= denominator) return 1;
        if (numerator * (_maximum + 1L) < 2 * denominator) return ExactMeanAccumulator.UnitRatio(2 * Unit, new BigInteger(_maximum + 1L));
        return ExactMeanAccumulator.UnitRatio(numerator * Unit, denominator);
    }
    internal (double Value, Signal Signal, double Efficiency, double Alpha) Next(double price, bool commit)
    {
        var current = ExactVarianceWindow.Units(price); var step = _started ? BigInteger.Abs(current - _previousPrice) : BigInteger.Zero;
        var full = _history.Count == _length; var old = full ? _history.Peek() : (Price: BigInteger.Zero, Step: BigInteger.Zero);
        var travel = _travel + step - old.Step;
        var efficiency = !full || travel.IsZero ? 0 : ExactMeanAccumulator.UnitRatio(BigInteger.Abs(current - old.Price) * Unit, travel);
        var alpha = Gain(efficiency); var average = new ExactMeanAccumulator(); average.Add(_previous); average.AddProduct(price, alpha); average.AddProduct(_previous, alpha, -1);
        var value = average.Mean(1); var spread = current - ExactVarianceWindow.Units(value); var change = spread - _spread;
        var signal = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit) { if (full) _history.Dequeue(); _history.Enqueue((current, step)); _travel = travel; _previousPrice = current; _previous = value; _spread = spread; _started = true; }
        return (value, signal, efficiency, alpha);
    }
    internal void Reset() { _history.Clear(); _travel = _previousPrice = _spread = default; _previous = 0; _started = false; }
}
