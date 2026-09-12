using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The window's standard deviation as a percentage of its own mean, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateCoefficientOfVariation</c>. The window holds the values
/// already final, so a preview bar is measured against them without joining them.
/// </remarks>
public sealed class CoefficientOfVariationState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public CoefficientOfVariationState(int length = 20)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CoefficientOfVariation;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double cv = 0;
        if (_window.Count + 1 >= _length)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window.
            var start = _window.Count - (_length - 1);
            double sum = 0;
            for (var i = start; i < _window.Count; i++)
            {
                sum += _window[i];
            }

            sum += value;

            var mean = sum / _length;
            double variance = 0;
            for (var i = start; i < _window.Count; i++)
            {
                var diff = _window[i] - mean;
                variance += diff * diff;
            }

            var currentDiff = value - mean;
            variance += currentDiff * currentDiff;

            var stdDev = Sqrt(variance / _length);
            cv = mean != 0 ? stdDev / mean * 100 : 0;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Cv", cv } };
        }

        return new StreamingIndicatorStateResult(cv, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The deviation of only those returns that fell short of a target, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateDownsideDeviation</c>. Each return needs the value before
/// it, so the window holds one more value than the window of returns it measures.
/// </remarks>
public sealed class DownsideDeviationState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly double _targetReturn;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public DownsideDeviationState(int length = 20, double targetReturn = 0)
    {
        _length = Math.Max(1, length);
        _targetReturn = targetReturn;
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.DownsideDeviation;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double downsideDeviation = 0;
        if (_window.Count >= _length)
        {
            double sumSquaredDownside = 0;
            var shortfalls = 0;
            for (var i = 1; i <= _length; i++)
            {
                var prevValue = _window[i - 1];
                var currentValue = i < _length ? _window[i] : value;
                var ret = prevValue > 0 ? (currentValue - prevValue) / prevValue : 0;
                if (ret < _targetReturn)
                {
                    var shortfall = ret - _targetReturn;
                    sumSquaredDownside += shortfall * shortfall;
                    shortfalls++;
                }
            }

            downsideDeviation = shortfalls > 0 ? Sqrt(sumSquaredDownside / shortfalls) : 0;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Dd", downsideDeviation } };
        }

        return new StreamingIndicatorStateResult(downsideDeviation, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// How lopsided the window is about its own mean, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateSkewness</c>: the population third moment over the cube
/// of the population standard deviation.
/// </remarks>
public sealed class SkewnessState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public SkewnessState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Skewness;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double skewness = 0;
        if (_window.Count + 1 >= _length)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window.
            var start = _window.Count - (_length - 1);
            double sum = 0;
            for (var i = start; i < _window.Count; i++)
            {
                sum += _window[i];
            }

            sum += value;

            var mean = sum / _length;
            double sumSquaredDev = 0;
            double sumCubedDev = 0;
            for (var i = start; i < _window.Count; i++)
            {
                var dev = _window[i] - mean;
                sumSquaredDev += dev * dev;
                sumCubedDev += dev * dev * dev;
            }

            var currentDev = value - mean;
            sumSquaredDev += currentDev * currentDev;
            sumCubedDev += currentDev * currentDev * currentDev;

            var stdDev = Sqrt(sumSquaredDev / _length);
            skewness = stdDev != 0 ? sumCubedDev / _length / (stdDev * stdDev * stdDev) : 0;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Skewness", skewness } };
        }

        return new StreamingIndicatorStateResult(skewness, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// How much of the window's movement a straight line accounts for, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRSquared</c>: the square of the correlation between the
/// window's values and the bar numbers they sit on.
/// </remarks>
public sealed class RSquaredState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public RSquaredState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RSquared;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double rSquared = 0;
        if (_window.Count + 1 >= _length)
        {
            var start = _window.Count - (_length - 1);
            double sumX = 0, sumY = 0, sumXy = 0, sumX2 = 0, sumY2 = 0;
            for (var j = 0; j < _length; j++)
            {
                double x = j;
                var y = j < _length - 1 ? _window[start + j] : value;
                sumX += x;
                sumY += y;
                sumXy += x * y;
                sumX2 += x * x;
                sumY2 += y * y;
            }

            var numerator = (_length * sumXy) - (sumX * sumY);
            var denominator = Sqrt(((_length * sumX2) - (sumX * sumX)) * ((_length * sumY2) - (sumY * sumY)));
            var r = denominator != 0 ? numerator / denominator : 0;
            rSquared = r * r;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "RSquared", rSquared } };
        }

        return new StreamingIndicatorStateResult(rSquared, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The share of the previous values that the current one stands above, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculatePercentRank</c>. The window holds only the bars before
/// this one, so a value is never ranked against itself.
/// </remarks>
public sealed class PercentRankState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public PercentRankState(int length = 100)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.PercentRank;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double percentRank = 0;
        if (_window.Count >= _length)
        {
            var below = 0;
            for (var i = 0; i < _length; i++)
            {
                if (_window[i] < value)
                {
                    below++;
                }
            }

            percentRank = (double)below / _length * 100;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "PercentRank", percentRank } };
        }

        return new StreamingIndicatorStateResult(percentRank, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The middle value of the window once sorted, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateMedianValue</c>. Until the window fills there is no
/// median to take, and the bar publishes its own value rather than zero.
/// </remarks>
public sealed class MedianValueState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly double[] _sorted;
    private readonly StreamingInputResolver _input;

    public MedianValueState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _sorted = new double[_length];
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.MedianValue;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double median;
        if (_window.Count + 1 < _length)
        {
            median = value;
        }
        else
        {
            var start = _window.Count - (_length - 1);
            for (var j = 0; j < _length - 1; j++)
            {
                _sorted[j] = _window[start + j];
            }

            _sorted[_length - 1] = value;
            Array.Sort(_sorted);
            median = _length % 2 == 0 ? (_sorted[(_length / 2) - 1] + _sorted[_length / 2]) / 2 : _sorted[_length / 2];
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "MedianValue", median } };
        }

        return new StreamingIndicatorStateResult(median, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}
