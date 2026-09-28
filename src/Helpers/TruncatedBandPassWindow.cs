namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TruncatedBandPassWindow
{
    private readonly int _cutoff;
    private readonly double _drive, _feedback, _decay;
    private readonly List<double> _values = new();
    internal TruncatedBandPassWindow(int length1, int length2, double bandwidth)
    {
        length1 = Math.Max(1, length1); _cutoff = Math.Max(1, length2);
        if (double.IsNaN(bandwidth) || double.IsInfinity(bandwidth)) throw new ArgumentOutOfRangeException(nameof(bandwidth));
        var phase = bandwidth * 2 * Math.PI / length1;
        if (double.IsInfinity(phase))
        { var angle = new ExactMeanAccumulator(); angle.AddProduct(bandwidth, 2 * Math.PI); phase = angle.Mean(length1); }
        if (double.IsNaN(phase) || double.IsInfinity(phase)) throw new ArgumentOutOfRangeException(nameof(bandwidth));
        var cosine = Math.Cos(phase);
        _decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        _drive = .5 * (1 - _decay);
        _feedback = Math.Cos(Math.Max(.01, Math.Min(.99, 2 * Math.PI / length1))) * (1 + _decay);
    }
    internal double Next(double value, bool commit)
    {
        double At(long lag) => lag == 0 ? value : lag <= _values.Count ? _values[_values.Count - (int)lag] : 0;
        RocBankValue first = default, second = default;
        // The unseen tail is identically zero; no full-period scratch array is needed.
        for (var lag = Math.Min((long)_cutoff, _values.Count + 1L); lag > 0; lag--)
        {
            var difference = new ExactMeanAccumulator(); difference.Add(At(lag - 1)); difference.Add(At(lag + 1), -1);
            var drive = RocBankValue.Round(difference).Multiply(_drive);
            var sum = new ExactMeanAccumulator(); drive.AddTo(ref sum); first.Multiply(_feedback).AddTo(ref sum);
            var total = new ExactMeanAccumulator(); RocBankValue.Round(sum).AddTo(ref total); second.Multiply(_decay).AddTo(ref total, -1);
            second = first; first = RocBankValue.Round(total);
        }
        if (commit) { if (_values.Count > _cutoff) _values.RemoveAt(0); _values.Add(value); }
        return first.Publish();
    }
    internal void Reset() => _values.Clear();
}
