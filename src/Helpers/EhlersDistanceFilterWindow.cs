using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class EhlersDistanceFilterWindow
{
    private readonly int _length, _lag;
    private readonly Queue<double> _prices = new();
    private readonly Queue<(double Price, BigInteger Weight)> _terms = new();
    private ExactMeanAccumulator _numerator;
    private BigInteger _mass;
    internal EhlersDistanceFilterWindow(int length, int lag) { _length = Math.Max(1, length); _lag = Math.Max(1, lag); }
    internal double Next(double price, bool commit)
    {
        var previous = _prices.Count == _lag ? _prices.Peek() : 0;
        var weight = BigInteger.Abs(ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(previous));
        var numerator = _numerator; var mass = _mass;
        if (_terms.Count == _length) { var oldest = _terms.Peek(); numerator.Add(oldest.Price, -oldest.Weight); mass -= oldest.Weight; }
        numerator.Add(price, weight); mass += weight;
        var denominator = new ExactMeanAccumulator(); denominator.Add(1d, mass);
        var output = mass.IsZero ? price : numerator.Ratio(denominator);
        if (commit)
        {
            if (_terms.Count == _length) _terms.Dequeue(); _terms.Enqueue((price,weight));
            if (_prices.Count == _lag) _prices.Dequeue(); _prices.Enqueue(price); _numerator = numerator; _mass = mass;
        }
        return output;
    }
    internal void Reset() { _prices.Clear(); _terms.Clear(); _numerator = default; _mass = default; }
}
