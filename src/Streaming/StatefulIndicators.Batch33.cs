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
[PrimaryOutput("Cv")]
public sealed class CoefficientOfVariationState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactCoefficientWindow _window;
    private readonly StreamingInputResolver _input;

    public CoefficientOfVariationState(int length = 20)
    {
        _window = new ExactCoefficientWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CoefficientOfVariation;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        var cv = _window.Next(value, isFinal);

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
[PrimaryOutput("Dd")]
public sealed class DownsideDeviationState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactDownsideWindow _window;
    private readonly StreamingInputResolver _input;

    public DownsideDeviationState(int length = 20, double targetReturn = 0)
    {
        _window = new ExactDownsideWindow(length, targetReturn);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.DownsideDeviation;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        var downsideDeviation = _window.Next(value, isFinal);

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
[PrimaryOutput("Skewness")]
public sealed class SkewnessState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactSkewnessWindow _window;
    private readonly StreamingInputResolver _input;

    public SkewnessState(int length = 14)
    {
        _window = new ExactSkewnessWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.Skewness;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        var skewness = _window.Next(value, isFinal);

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
[PrimaryOutput("RSquared")]
public sealed class RSquaredState : IStreamingIndicatorState, IDisposable
{
    private readonly ExactRSquaredWindow _window;
    private readonly StreamingInputResolver _input;

    public RSquaredState(int length = 14)
    {
        _window = new ExactRSquaredWindow(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RSquared;

    public void Reset()
    {
        _window.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        var rSquared = _window.Next(value, isFinal);

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
[PrimaryOutput("PercentRank")]
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

            percentRank = 100d * below / _length;
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
[PrimaryOutput("MedianValue")]
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
            median = _length % 2 == 0 ? PriceMean.Of(_sorted[(_length / 2) - 1], _sorted[_length / 2]) : _sorted[_length / 2];
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
