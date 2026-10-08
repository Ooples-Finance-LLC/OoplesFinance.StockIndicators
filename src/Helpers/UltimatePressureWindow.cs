using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class UltimatePressureWindow
{
    private readonly int[] _lengths;
    private readonly Queue<(BigInteger Pressure, BigInteger Range)>[] _history = { new(), new(), new() };
    private readonly BigInteger[] _pressure = new BigInteger[3], _range = new BigInteger[3];
    private double _previous;
    private bool _hasPrevious;
    internal UltimatePressureWindow(int first, int second, int third) => _lengths = new[] { Math.Max(1, first), Math.Max(1, second), Math.Max(1, third) };
    internal double Next(double high, double low, double close, bool commit)
    {
        var previous = _hasPrevious ? _previous : close;
        var lower = ExactVarianceWindow.Units(Math.Min(low, previous)); var upper = ExactVarianceWindow.Units(Math.Max(high, previous));
        var pressure = RocBankValue.RoundUnits(ExactVarianceWindow.Units(close) - lower, BigInteger.One);
        var range = RocBankValue.RoundUnits(upper - lower, BigInteger.One); var blend = new ExactMeanAccumulator();
        for (var slot = 0; slot < 3; slot++)
        {
            var expired = _history[slot].Count == _lengths[slot] ? _history[slot].Peek() : default;
            var p = _pressure[slot] + pressure - expired.Pressure; var r = _range[slot] + range - expired.Range;
            var ratio = r.IsZero ? BigInteger.Zero : RocBankValue.RoundUnits(p << 1074, r);
            blend.Add(double.Epsilon, ratio * (slot == 0 ? 4 : slot == 1 ? 2 : 1));
            if (commit)
            {
                if (_history[slot].Count == _lengths[slot]) _history[slot].Dequeue();
                _history[slot].Enqueue((pressure, range)); _pressure[slot] = p; _range[slot] = r;
            }
        }
        var result = RocBankValue.Round(blend, count: 7).Multiply(100).Publish();
        if (commit) { _previous = close; _hasPrevious = true; }
        return Math.Max(0, Math.Min(100, result));
    }
    internal void Reset() { foreach (var h in _history) h.Clear(); Array.Clear(_pressure, 0, 3); Array.Clear(_range, 0, 3); _previous = 0; _hasPrevious = false; }
}
