using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

// The lagged caller price drives a rounded cumulative deviation. Two partial
// means follow the gated target; neither uses the momentum delay as its period.
internal sealed class AutonomousRecursiveWindow
{
    private readonly int _length, _momentumLength; private readonly BigInteger _gamma;
    private readonly Queue<BigInteger> _prices = new(), _targets = new(), _firstMeans = new();
    private BigInteger _deviationSum, _targetSum, _firstSum, _previous, _spread; private long _count;
    internal AutonomousRecursiveWindow(int length, int momentumLength, double gamma)
    {
        if (double.IsNaN(gamma) || double.IsInfinity(gamma)) throw new ArgumentOutOfRangeException(nameof(gamma));
        _length = Math.Max(1, length); _momentumLength = Math.Max(1, momentumLength); _gamma = ExactVarianceWindow.Units(gamma);
    }
    private static BigInteger Round(BigInteger value) => RocBankValue.RoundUnits(value, BigInteger.One);
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var current = ExactVarianceWindow.Units(price); var previous = _count == 0 ? current : _previous;
        var lagged = _prices.Count == _momentumLength ? _prices.Peek() : BigInteger.Zero;
        var error = Round(BigInteger.Abs(lagged - previous)); var deviationSum = Round(_deviationSum + error);
        var radius = _count == 0 ? BigInteger.Zero : RocBankValue.RoundUnits(RocBankValue.RoundUnits(deviationSum, new BigInteger(_count)) * _gamma, BigInteger.One << 1074);
        var upper = Round(previous + radius); var lower = Round(previous - radius);
        var target = current > upper ? Round(current + radius) : current < lower ? Round(current - radius) : previous;
        var full = _targets.Count == _length; var count = full ? _length : _targets.Count + 1;
        var targetSum = _targetSum + target - (full ? _targets.Peek() : BigInteger.Zero);
        var first = RocBankValue.RoundUnits(targetSum, new BigInteger(count));
        var firstSum = _firstSum + first - (full ? _firstMeans.Peek() : BigInteger.Zero);
        var second = RocBankValue.RoundUnits(firstSum, new BigInteger(count));
        var spread = current - second; var change = spread - _spread;
        var signal = spread.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : spread.Sign < 0 && change.Sign < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None;
        if (commit)
        {
            if (_prices.Count == _momentumLength) _prices.Dequeue(); _prices.Enqueue(current);
            if (full) { _targets.Dequeue(); _firstMeans.Dequeue(); } _targets.Enqueue(target); _firstMeans.Enqueue(first);
            _deviationSum = deviationSum; _targetSum = targetSum; _firstSum = firstSum; _previous = second; _spread = spread; _count++;
        }
        return (ExactMeanAccumulator.UnitRatio(second, BigInteger.One), signal);
    }
    internal void Reset() { _prices.Clear(); _targets.Clear(); _firstMeans.Clear(); _deviationSum = _targetSum = _firstSum = _previous = _spread = BigInteger.Zero; _count = 0; }
}
