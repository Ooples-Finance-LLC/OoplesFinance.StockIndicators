namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class BelkhayateWindow
{
    private readonly Queue<(RocBankValue Middle, RocBankValue Range)> _history = new();
    private ExactMeanAccumulator _middle, _range;
    internal double Next(double high, double low, double close, bool commit)
    {
        var pair = new ExactMeanAccumulator(); pair.Add(high); pair.Add(low); var midpoint = RocBankValue.Round(pair, count: 2);
        var difference = new ExactMeanAccumulator(); difference.Add(high); difference.Add(low, -1); var range = RocBankValue.Round(difference);
        var middleTotal = _middle; var rangeTotal = _range;
        if (_history.Count == 5) { var expired = _history.Peek(); expired.Middle.AddTo(ref middleTotal, -1); expired.Range.AddTo(ref rangeTotal, -1); }
        midpoint.AddTo(ref middleTotal); range.AddTo(ref rangeTotal);
        var center = RocBankValue.Round(middleTotal, count: 5); var scale = RocBankValue.Round(rangeTotal, count: 5).Multiply(.2);
        var result = 0d;
        if (scale.Mantissa != 0)
        {
            var displacement = new ExactMeanAccumulator(); displacement.Add(close); center.AddTo(ref displacement, -1);
            var numerator = new ExactMeanAccumulator(); RocBankValue.Round(displacement).AddTo(ref numerator); var denominator = new ExactMeanAccumulator(); scale.AddTo(ref denominator);
            result = numerator.Ratio(denominator);
        }
        if (commit) { if (_history.Count == 5) _history.Dequeue(); _history.Enqueue((midpoint, range)); _middle = middleTotal; _range = rangeTotal; }
        return result;
    }
    internal void Reset() { _history.Clear(); _middle = _range = default; }
}
