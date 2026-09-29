namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Two trailing stops with persistent direction and extended intermediate values.</summary>
internal struct OptimizedTrendStops
{
    private bool _initialized, _rising;
    private RocBankValue _longStop, _shortStop;
    private static int Compare(RocBankValue left, RocBankValue right)
    { var sum = new ExactMeanAccumulator(); left.AddTo(ref sum); right.AddTo(ref sum, -1); return sum.Sign; }
    internal RocBankValue Next(RocBankValue average, double percent)
    {
        var product = new ExactMeanAccumulator(); product.AddProduct(Math.Abs(average.Mantissa), percent); product.ScaleByPowerOfTwo(average.UpperShift);
        var distance = RocBankValue.Round(product, count: 100);
        RocBankValue Candidate(int sign) { var sum = new ExactMeanAccumulator(); average.AddTo(ref sum); distance.AddTo(ref sum, sign); return RocBankValue.Round(sum); }
        var lower = Candidate(-1); var upper = Candidate(1);
        if (!_initialized) { _initialized = true; _rising = true; }
        else
        {
            if (_rising && Compare(average, _longStop) < 0) _rising = false;
            else if (!_rising && Compare(average, _shortStop) > 0) _rising = true;
            if (Compare(average, _longStop) > 0 && Compare(lower, _longStop) < 0) lower = _longStop;
            if (Compare(average, _shortStop) < 0 && Compare(upper, _shortStop) > 0) upper = _shortStop;
        }
        _longStop = lower; _shortStop = upper; var stop = _rising ? lower : upper;
        var output = new ExactMeanAccumulator(); output.AddProduct(stop.Mantissa, percent, Compare(average, stop) > 0 ? 1 : -1); output.ScaleByPowerOfTwo(stop.UpperShift); stop.AddTo(ref output, 200);
        return RocBankValue.Round(output, count: 200);
    }
}
