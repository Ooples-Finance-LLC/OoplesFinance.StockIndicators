namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TrendContinuationWindow : IDisposable
{
    private readonly PooledRingBuffer<(RocBankValue Plus, RocBankValue Minus)> _terms;
    private ExactMeanAccumulator _plusSum, _minusSum;
    private RocBankValue _plusRun, _minusRun;
    private double _previous;
    private bool _hasPrevious;
    internal TrendContinuationWindow(int length) => _terms = new(Math.Max(1, length));
    private static RocBankValue Accumulate(RocBankValue change, RocBankValue previous)
    {
        if (change.Mantissa == 0) return default;
        var total = new ExactMeanAccumulator(); change.AddTo(ref total); previous.AddTo(ref total);
        return RocBankValue.Round(total);
    }
    internal (double Plus, double Minus) Next(double price, bool commit)
    {
        var difference = new ExactMeanAccumulator();
        if (_hasPrevious) { difference.Add(price); difference.Add(_previous, -1); }
        var change = RocBankValue.Round(difference);
        var up = change.Mantissa > 0 ? change : default;
        var down = change.Mantissa < 0 ? new RocBankValue(-change.Mantissa, change.UpperShift) : default;
        var plusRun = Accumulate(up, _plusRun); var minusRun = Accumulate(down, _minusRun);
        var positive = new ExactMeanAccumulator(); up.AddTo(ref positive); minusRun.AddTo(ref positive, -1);
        var negative = new ExactMeanAccumulator(); down.AddTo(ref negative); plusRun.AddTo(ref negative, -1);
        var plusTerm = RocBankValue.Round(positive); var minusTerm = RocBankValue.Round(negative);
        var plusSum = _plusSum; var minusSum = _minusSum;
        if (_terms.Count == _terms.Capacity) { _terms[0].Plus.AddTo(ref plusSum, -1); _terms[0].Minus.AddTo(ref minusSum, -1); }
        plusTerm.AddTo(ref plusSum); minusTerm.AddTo(ref minusSum);
        if (commit)
        {
            _terms.TryAdd((plusTerm, minusTerm), out _); _plusSum = plusSum; _minusSum = minusSum;
            _plusRun = plusRun; _minusRun = minusRun; _previous = price; _hasPrevious = true;
        }
        return (plusSum.Mean(1), minusSum.Mean(1));
    }
    internal void Reset() { _terms.Clear(); _plusSum = default; _minusSum = default; _plusRun = default; _minusRun = default; _previous = 0; _hasPrevious = false; }
    public void Dispose() => _terms.Dispose();
}
