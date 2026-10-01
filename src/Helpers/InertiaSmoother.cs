using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

// Inertia's bounded RVI input needs only observed history, even for huge periods.
internal sealed class InertiaSmoother
{
    private readonly MovingAvgType _kind;
    private readonly int _length;
    private readonly Queue<BigInteger> _history = new();
    private BigInteger _sum, _weighted, _previous;
    private int _count;
    internal InertiaSmoother(MovingAvgType kind, int length) { _kind = kind; _length = Math.Max(1, length); }
    internal static bool Supports(MovingAvgType kind) => kind == MovingAvgType.LinearRegression || StrengthWindow.Supports(kind);
    internal double Next(double input, bool final)
    {
        var value = ExactVarianceWindow.Units(input); var sum = _sum; var weighted = _weighted;
        var rolling = _kind is MovingAvgType.LinearRegression or MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
        var full = _history.Count == _length; var n = new BigInteger(full ? _length : _history.Count + 1);
        BigInteger numerator, denominator;
        if (rolling)
        {
            var expired = full ? _history.Peek() : BigInteger.Zero;
            weighted = _kind == MovingAvgType.LinearRegression
                ? full ? weighted - sum + expired + (n - 1) * value : weighted + (n - 1) * value
                : weighted - sum + _length * value;
            sum += value - expired;
            if (_kind == MovingAvgType.LinearRegression)
            {
                var spread = n * n - 1; var covariance = 2 * weighted - (n - 1) * sum;
                numerator = n.IsOne ? value : sum * spread + 3 * covariance * (n - 1);
                denominator = n.IsOne ? BigInteger.One : n * spread;
            }
            else
            {
                numerator = _kind == MovingAvgType.WeightedMovingAverage ? weighted : n < _length ? BigInteger.Zero : sum;
                denominator = _kind == MovingAvgType.WeightedMovingAverage ? new BigInteger(_length) * (_length + 1L) / 2 : _length;
            }
        }
        else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
        { sum += value; numerator = sum; denominator = _count + 1; }
        else
        {
            var ema = _kind == MovingAvgType.ExponentialMovingAverage;
            numerator = (_length - 1L) * _previous + (ema ? 2 : 1) * value;
            denominator = ema ? _length + 1L : _length;
        }
        var result = ExactMeanAccumulator.UnitRatio(numerator, denominator);
        if (final)
        {
            _sum = sum; _weighted = weighted; _previous = ExactVarianceWindow.Units(result);
            if (_count < _length) _count++;
            if (rolling) { if (full) _history.Dequeue(); _history.Enqueue(value); }
        }
        return result;
    }
    internal static double[] Compute(IReadOnlyList<double> input, MovingAvgType kind, int length)
    { var smoother = new InertiaSmoother(kind, length); return input.Select(v => smoother.Next(v, true)).ToArray(); }
    internal static Signal Trade(double value, double previous, double older)
    {
        var change = ExactVarianceWindow.Units(value) - ExactVarianceWindow.Units(previous);
        var before = ExactVarianceWindow.Units(previous) - ExactVarianceWindow.Units(older);
        return change.Sign > 0 && change > before ? Signal.StrongBuy : change.Sign < 0 && change < before ? Signal.StrongSell
            : change.Sign > 0 ? Signal.Buy : change.Sign < 0 ? Signal.Sell : Signal.None;
    }
    internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; }
}
