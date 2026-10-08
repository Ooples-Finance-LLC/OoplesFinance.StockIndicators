namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class VossPredictiveWindow
{
    private readonly int _order;
    private readonly double _drive, _feedback, _decay;
    private readonly List<RocBankValue> _predictions = new();
    private RocBankValue _first, _second;
    private double _previous, _older;
    private int _startup;
    internal VossPredictiveWindow(int length, double predict, double bandwidth)
    {
        if (double.IsNaN(predict) || double.IsInfinity(predict)) throw new ArgumentOutOfRangeException(nameof(predict));
        if (double.IsNaN(bandwidth) || double.IsInfinity(bandwidth)) throw new ArgumentOutOfRangeException(nameof(bandwidth));
        length = Math.Max(1, length); _order = (int)Math.Max(2, Math.Min(530, Math.Ceiling(3 * predict)));
        var phase = bandwidth * 2 * Math.PI / length;
        if (double.IsInfinity(phase)) { var angle = new ExactMeanAccumulator(); angle.AddProduct(bandwidth, 2 * Math.PI); phase = angle.Mean(length); }
        if (double.IsInfinity(phase)) throw new ArgumentOutOfRangeException(nameof(bandwidth));
        var cosine = Math.Cos(phase); _decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        _drive = .5 * (1 - _decay); _feedback = Math.Cos(2 * Math.PI / length) * (1 + _decay);
    }
    internal (double Voss, double Filter) Next(double value, bool commit)
    {
        RocBankValue filter = default;
        if (_startup >= 6)
        {
            var delta = new ExactMeanAccumulator(); delta.Add(value); delta.Add(_older, -1);
            var sum = new ExactMeanAccumulator(); RocBankValue.Round(delta).Multiply(_drive).AddTo(ref sum); _first.Multiply(_feedback).AddTo(ref sum);
            var difference = new ExactMeanAccumulator(); RocBankValue.Round(sum).AddTo(ref difference); _second.Multiply(_decay).AddTo(ref difference, -1);
            filter = RocBankValue.Round(difference);
        }
        RocBankValue weighted = default;
        for (var j = 0; j < _predictions.Count; j++)
        {
            var weight = (double)(_order - _predictions.Count + j + 1) / _order;
            var sum = new ExactMeanAccumulator(); weighted.AddTo(ref sum); _predictions[j].Multiply(weight).AddTo(ref sum); weighted = RocBankValue.Round(sum);
        }
        var result = new ExactMeanAccumulator(); filter.Multiply((3d + _order) / 2).AddTo(ref result); weighted.AddTo(ref result, -1);
        var prediction = RocBankValue.Round(result);
        if (commit)
        {
            _second = _first; _first = filter; _older = _previous; _previous = value; _startup = Math.Min(6, _startup + 1);
            if (_predictions.Count == _order) _predictions.RemoveAt(0); _predictions.Add(prediction);
        }
        return (prediction.Publish(), filter.Publish());
    }
    internal void Reset() { _predictions.Clear(); _first = _second = default; _previous = _older = 0; _startup = 0; }
}
