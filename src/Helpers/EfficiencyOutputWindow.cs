namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EfficiencyOutputWindow
{
    private readonly int _length;
    private readonly bool _autoLine;
    private readonly double _fast, _slow;
    private readonly Queue<double> _prices = new();
    private readonly Queue<ExactMeanAccumulator> _changes = new();
    private ExactMeanAccumulator _travel;
    private double _previous;
    private RocBankValue _output;
    private long _count;
    internal EfficiencyOutputWindow(int length, bool autoLine = false, double fast = .0001, double slow = .005)
    {
        if (double.IsNaN(fast) || double.IsInfinity(fast)) throw new ArgumentOutOfRangeException(nameof(fast));
        if (double.IsNaN(slow) || double.IsInfinity(slow)) throw new ArgumentOutOfRangeException(nameof(slow));
        _length = Math.Max(1, length); _autoLine = autoLine; _fast = fast; _slow = slow;
    }
    private static ExactMeanAccumulator Absolute(ExactMeanAccumulator value)
    {
        if (value.Sign >= 0) return value;
        var positive = new ExactMeanAccumulator(); positive.Subtract(value); return positive;
    }
    internal double Next(double price, bool commit)
    {
        var change = new ExactMeanAccumulator(); change.Add(price); change.Add(_previous, -1); change = Absolute(change);
        var travel = _travel; if (_changes.Count == _length) travel.Subtract(_changes.Peek());
        var negative = new ExactMeanAccumulator(); negative.Subtract(change); travel.Subtract(negative);
        var displacement = new ExactMeanAccumulator();
        if (_prices.Count == _length) { displacement.Add(price); displacement.Add(_prices.Peek(), -1); }
        var efficiency = Absolute(displacement).Ratio(travel);
        RocBankValue output;
        if (_autoLine)
        {
            var threshold = new ExactMeanAccumulator(); threshold.Add(_slow); threshold.AddProduct(_fast, efficiency); threshold.AddProduct(_slow, -efficiency);
            var deviation = RocBankValue.Round(threshold);
            var distance = new ExactMeanAccumulator(); distance.Add(price); _output.AddTo(ref distance, -1); distance = Absolute(distance);
            deviation.AddTo(ref distance, -1);
            output = _count < 9 || distance.Sign > 0 ? new RocBankValue(price) : _output;
        }
        else
        {
            var increment = RocBankValue.Round(displacement).Multiply(efficiency);
            var total = new ExactMeanAccumulator(); _output.AddTo(ref total); increment.AddTo(ref total); output = RocBankValue.Round(total);
        }
        if (commit)
        {
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price);
            if (_changes.Count == _length) _changes.Dequeue(); _changes.Enqueue(change);
            _travel = travel; _previous = price; _output = output; _count++;
        }
        return output.Publish();
    }
    internal void Reset() { _prices.Clear(); _changes.Clear(); _travel = default; _previous = 0; _output = default; _count = 0; }
}
