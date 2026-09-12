using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The Yang-Zhang volatility, bar by bar.
/// </summary>
/// <remarks>
/// <para>
/// The streaming twin of <c>Calculations.CalculateYangZhangVolatility</c>. Each bar contributes three terms
/// - the overnight jump, the move from open to close, and the Rogers-Satchell reading of its range - and the
/// window holds those terms rather than the prices they came from.
/// </para>
/// <para>
/// A term the batch engine cannot take, because the price it would divide by is zero, is left out of both
/// the mean and the sum of squares while the divisors stay the window's length. Such a term is held here as
/// NaN and skipped, rather than stored as zero, which would count a deviation that the batch engine never
/// counts.
/// </para>
/// </remarks>
public sealed class YangZhangVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _k;
    private readonly double _annualisationFactor = Sqrt(252);
    private readonly PooledRingBuffer<double> _overnight;
    private readonly PooledRingBuffer<double> _openToClose;
    private readonly PooledRingBuffer<double> _rogersSatchell;
    private readonly StreamingInputResolver _input;
    private double _prevClose;
    private bool _hasPrev;

    public YangZhangVolatilityState(int length = 20)
    {
        _length = Math.Max(1, length);
        _k = 0.34 / (1 + ((double)(_length + 1) / (_length - 1)));
        _overnight = new PooledRingBuffer<double>(_length);
        _openToClose = new PooledRingBuffer<double>(_length);
        _rogersSatchell = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.YangZhangVolatility;

    public void Reset()
    {
        _overnight.Clear();
        _openToClose.Clear();
        _rogersSatchell.Clear();
        _prevClose = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var overnight = _hasPrev && _prevClose != 0 ? Log(bar.Open / _prevClose) : double.NaN;
        var openToClose = bar.Open != 0 ? Log(value / bar.Open) : double.NaN;

        var logHc = value != 0 ? Log(bar.High / value) : 0;
        var logHo = bar.Open != 0 ? Log(bar.High / bar.Open) : 0;
        var logLc = value != 0 ? Log(bar.Low / value) : 0;
        var logLo = bar.Open != 0 ? Log(bar.Low / bar.Open) : 0;
        var rogersSatchell = (logHc * logHo) + (logLc * logLo);

        double volatility = 0;
        if (_overnight.Count >= _length)
        {
            var start = _overnight.Count - (_length - 1);
            var overnightVariance = SampleVariance(_overnight, start, overnight);
            var openToCloseVariance = SampleVariance(_openToClose, start, openToClose);

            // Summed oldest first with this bar last, as the batch engine sums its window.
            double rogersSatchellSum = 0;
            for (var i = start; i < _rogersSatchell.Count; i++)
            {
                rogersSatchellSum += _rogersSatchell[i];
            }

            rogersSatchellSum += rogersSatchell;

            var rogersSatchellVariance = rogersSatchellSum / _length;
            var yangZhangVariance = overnightVariance + (_k * openToCloseVariance)
                + ((1 - _k) * rogersSatchellVariance);
            volatility = Sqrt(yangZhangVariance) * _annualisationFactor;
        }

        if (isFinal)
        {
            _overnight.TryAdd(overnight, out _);
            _openToClose.TryAdd(openToClose, out _);
            _rogersSatchell.TryAdd(rogersSatchell, out _);
            _prevClose = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Yzv", volatility } };
        }

        return new StreamingIndicatorStateResult(volatility, outputs);
    }

    /// <summary>
    /// The variance of the window's terms about their own mean, leaving out the terms that could not be
    /// taken, with the mean over the window's length and the variance over one less, as the batch engine
    /// divides them.
    /// </summary>
    private double SampleVariance(PooledRingBuffer<double> window, int start, double current)
    {
        double mean = 0;
        for (var i = start; i < window.Count; i++)
        {
            if (!double.IsNaN(window[i]))
            {
                mean += window[i];
            }
        }

        if (!double.IsNaN(current))
        {
            mean += current;
        }

        mean /= _length;

        double sum = 0;
        for (var i = start; i < window.Count; i++)
        {
            if (!double.IsNaN(window[i]))
            {
                var diff = window[i] - mean;
                sum += diff * diff;
            }
        }

        if (!double.IsNaN(current))
        {
            var currentDiff = current - mean;
            sum += currentDiff * currentDiff;
        }

        return sum / (_length - 1);
    }

    public void Dispose()
    {
        _overnight.Dispose();
        _openToClose.Dispose();
        _rogersSatchell.Dispose();
    }
}

/// <summary>
/// The average true range as a percentage of the price, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateNormalizedAverageTrueRange</c>. It wraps
/// <see cref="AverageTrueRangeState"/> rather than smoothing a true range of its own, so the range it
/// divides is the one the batch engine averages.
/// </remarks>
public sealed class NormalizedAverageTrueRangeState : IStreamingIndicatorState, IDisposable
{
    private readonly AverageTrueRangeState _averageTrueRange;
    private readonly StreamingInputResolver _input;

    public NormalizedAverageTrueRangeState(int length = 14)
    {
        _averageTrueRange = new AverageTrueRangeState(Math.Max(1, length));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.NormalizedAverageTrueRange;

    public void Reset()
    {
        _averageTrueRange.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var atr = _averageTrueRange.Update(bar, isFinal, includeOutputs: false).Value;
        var natr = value != 0 ? atr / value * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Natr", natr } };
        }

        return new StreamingIndicatorStateResult(natr, outputs);
    }

    public void Dispose()
    {
        _averageTrueRange.Dispose();
    }
}
