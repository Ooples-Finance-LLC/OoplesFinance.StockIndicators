using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Helpers;

// Cache candle terms, but retain the core's oldest-first floating-point folds.
// A rolling subtraction would change output bits after the first expiry.
internal sealed class OhlcVolatilityWindow : IDisposable
{
    internal enum Estimator { Parkinson, RogersSatchell, YangZhang }
    private readonly Estimator _estimator;
    private readonly int _length;
    private readonly PooledRingBuffer<Terms>? _window;
    private readonly double _annual = Math.Sqrt(252);
    private readonly double _factor, _weight;
    private double _previousClose;
    private bool _hasPrevious;

    internal OhlcVolatilityWindow(Estimator estimator, int length, int count)
    {
        _estimator = estimator;
        _length = estimator == Estimator.YangZhang ? Math.Max(2, length) : length;
        // Yang-Zhang skips the first candle and needs one extra observation.
        if (_length > count || estimator == Estimator.YangZhang && _length == count) return;
        _window = new(_length);
        if (estimator == Estimator.Parkinson) _factor = 1 / (4 * _length * Math.Log(2));
        if (estimator == Estimator.YangZhang)
            _weight = .34 / (1.34 + (double)(_length + 1) / (_length - 1));
    }

    private readonly record struct Terms(double Range, double Overnight, double Intraday, bool HasOvernight, bool HasIntraday);

    internal double Next(in Bar bar)
    {
        if (_window is null) return 0;
        if (_estimator == Estimator.YangZhang && !_hasPrevious)
        {
            _previousClose = bar.Close;
            _hasPrevious = true;
            return 0;
        }

        Terms terms;
        if (_estimator == Estimator.Parkinson)
        {
            var log = bar.Low != 0 ? StableLogRatio.OfSameSign(bar.High, bar.Low) : 0;
            terms = new(log * log, 0, 0, false, false);
        }
        else
        {
            var hc = bar.Close != 0 ? StableLogRatio.OfSameSign(bar.High, bar.Close) : 0;
            var ho = bar.Open != 0 ? StableLogRatio.OfSameSign(bar.High, bar.Open) : 0;
            var lc = bar.Close != 0 ? StableLogRatio.OfSameSign(bar.Low, bar.Close) : 0;
            var lo = bar.Open != 0 ? StableLogRatio.OfSameSign(bar.Low, bar.Open) : 0;
            var range = hc * ho + lc * lo;
            var overnightDefined = _estimator == Estimator.YangZhang && _previousClose != 0;
            var intradayDefined = _estimator == Estimator.YangZhang && bar.Open != 0;
            terms = new(range,
                overnightDefined ? StableLogRatio.OfSameSign(bar.Open, _previousClose) : 0,
                intradayDefined ? StableLogRatio.OfSameSign(bar.Close, bar.Open) : 0,
                overnightDefined, intradayDefined);
        }
        _previousClose = bar.Close;
        _window.TryAdd(terms, out _);
        if (_window.Count < _length) return 0;

        double rangeSum = 0, overnightMean = 0, intradayMean = 0;
        for (var i = 0; i < _length; i++)
        {
            var point = _window[i];
            rangeSum += point.Range;
            if (point.HasOvernight) overnightMean += point.Overnight;
            if (point.HasIntraday) intradayMean += point.Intraday;
        }
        if (_estimator == Estimator.Parkinson) return Math.Sqrt(_factor * rangeSum) * _annual;
        if (_estimator == Estimator.RogersSatchell) return Math.Sqrt(Math.Max(0, rangeSum / _length)) * _annual;

        overnightMean /= _length;
        intradayMean /= _length;
        double overnightSum = 0, intradaySum = 0;
        for (var i = 0; i < _length; i++)
        {
            var point = _window[i];
            // Undefined denominators omit the residual, but keep the full divisors.
            if (point.HasOvernight) overnightSum += (point.Overnight - overnightMean) * (point.Overnight - overnightMean);
            if (point.HasIntraday) intradaySum += (point.Intraday - intradayMean) * (point.Intraday - intradayMean);
        }
        var overnightVariance = overnightSum / (_length - 1);
        var intradayVariance = intradaySum / (_length - 1);
        var rangeVariance = rangeSum / _length;
        var variance = overnightVariance + _weight * intradayVariance + (1 - _weight) * rangeVariance;
        return Math.Sqrt(Math.Max(0, variance)) * _annual;
    }

    internal void Reset() { _window?.Clear(); _previousClose = 0; _hasPrevious = false; }
    public void Dispose() => _window?.Dispose();
}
