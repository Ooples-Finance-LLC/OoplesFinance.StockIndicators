namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EhlersKaufmanWindow
{
    private readonly int _length;
    private readonly Queue<double> _prices = new();
    private readonly Queue<ExactMeanAccumulator> _changes = new();
    private ExactMeanAccumulator _travel;
    private double _previous, _average;
    internal EhlersKaufmanWindow(int length) => _length = Math.Max(1, length);
    private static ExactMeanAccumulator Distance(double left, double right)
    {
        var result = new ExactMeanAccumulator(); result.Add(left); result.Add(right, -1);
        if (result.Sign < 0) { var positive = new ExactMeanAccumulator(); positive.Subtract(result); return positive; }
        return result;
    }
    internal double Next(double price, bool commit)
    {
        var change = Distance(price, _previous); var travel = _travel;
        if (_changes.Count == _length) travel.Subtract(_changes.Peek());
        var negative = new ExactMeanAccumulator(); negative.Subtract(change); travel.Subtract(negative);
        var prior = _length == 1 ? price : _prices.Count == _length - 1 ? _prices.Peek() : 0;
        var efficiency = Math.Min(1, Distance(price, prior).Ratio(travel));
        var root = .6667 * efficiency + .0645; var gain = root * root;
        var next = new ExactMeanAccumulator(); next.Add(_average); next.AddProduct(price, gain); next.AddProduct(_average, -gain);
        var average = next.Mean(1);
        if (commit)
        {
            if (_length > 1) { if (_prices.Count == _length - 1) _prices.Dequeue(); _prices.Enqueue(price); }
            if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(change);
            _travel = travel; _previous = price; _average = average;
        }
        return average;
    }
    internal void Reset() { _prices.Clear(); _changes.Clear(); _travel = default; _previous = 0; _average = 0; }
}
