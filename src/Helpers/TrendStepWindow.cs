using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TrendStepWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    private BigInteger _sum, _squares;
    private double _previous;
    internal TrendStepWindow(int length) => _length = Math.Max(1, length);
    private static RocBankValue Bound(double previous, RocBankValue deviation, int sign)
    { var sum = new ExactMeanAccumulator(); sum.Add(previous); deviation.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
    private static int Compare(double price, RocBankValue bound)
    { var sum = new ExactMeanAccumulator(); sum.Add(price); bound.AddTo(ref sum, -1); return sum.Sign; }
    internal double Next(double price, bool commit)
    {
        var full = _prices.Count == _length; var current = ExactVarianceWindow.Units(price); var expired = full ? ExactVarianceWindow.Units(_prices.Peek()) : BigInteger.Zero;
        var sum = _sum + current - expired; var squares = _squares + current * current - expired * expired; var n = new BigInteger(_length);
        var deviation = _prices.Count + 1L < _length ? 0 : ExactPopulationDeviation.RootRatio(n * squares - sum * sum, n * n);
        var twice = new RocBankValue(deviation).Multiply(2);
        // The first length observations track price; a full window alone does not end startup one bar early.
        var result = !full || Compare(price, Bound(_previous, twice, 1)) > 0 || Compare(price, Bound(_previous, twice, -1)) < 0 ? price : _previous;
        if (commit) { _sum = sum; _squares = squares; if (full) _prices.Dequeue(); _prices.Enqueue(price); _previous = result; }
        return result;
    }
    internal void Reset() { _prices.Clear(); _sum = _squares = default; _previous = 0; }
    public void Dispose() { }
}
