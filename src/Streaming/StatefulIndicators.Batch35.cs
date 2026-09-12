using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The annualised deviation of the series' logarithmic returns, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateCloseToCloseVolatility</c>. The window holds one return
/// per bar, including the first bar's return of zero, so the two engines measure the same window.
/// </remarks>
public sealed class CloseToCloseVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _returns;
    private readonly StreamingInputResolver _input;
    private readonly double _annualisationFactor = Sqrt(252);
    private double _prevValue;
    private bool _hasPrev;

    public CloseToCloseVolatilityState(int length = 20)
    {
        _length = Math.Max(1, length);
        _returns = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.CloseToCloseVolatility;

    public void Reset()
    {
        _returns.Clear();
        _prevValue = 0;
        _hasPrev = false;
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var currentReturn = _hasPrev && _prevValue != 0 ? Log(value / _prevValue) : 0;

        double volatility = 0;
        if (_returns.Count + 1 >= _length)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window.
            var start = _returns.Count - (_length - 1);
            double sum = 0;
            for (var i = start; i < _returns.Count; i++)
            {
                sum += _returns[i];
            }

            sum += currentReturn;

            var mean = sum / _length;
            double variance = 0;
            for (var i = start; i < _returns.Count; i++)
            {
                var diff = _returns[i] - mean;
                variance += diff * diff;
            }

            var currentDiff = currentReturn - mean;
            variance += currentDiff * currentDiff;

            volatility = Sqrt(variance / _length) * _annualisationFactor;
        }

        if (isFinal)
        {
            _returns.TryAdd(currentReturn, out _);
            _prevValue = value;
            _hasPrev = true;
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Ctcv", volatility } };
        }

        return new StreamingIndicatorStateResult(volatility, outputs);
    }

    public void Dispose()
    {
        _returns.Dispose();
    }
}

/// <summary>
/// Parkinson's volatility, read from the range each bar travelled, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateParkinsonVolatility</c>. The window holds each bar's
/// squared logarithm of high over low, which is all the estimator needs from a bar.
/// </remarks>
public sealed class ParkinsonVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;
    private readonly double _factor;
    private readonly double _annualisationFactor = Sqrt(252);

    public ParkinsonVolatilityState(int length = 20)
    {
        _length = Math.Max(1, length);
        _factor = 1 / (4 * _length * Log(2));
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.ParkinsonVolatility;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        _ = _input.GetValue(bar);
        var logRatio = bar.Low != 0 ? Log(bar.High / bar.Low) : 0;
        var squared = logRatio * logRatio;

        double volatility = 0;
        if (_window.Count + 1 >= _length)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window.
            var start = _window.Count - (_length - 1);
            double sum = 0;
            for (var i = start; i < _window.Count; i++)
            {
                sum += _window[i];
            }

            sum += squared;

            volatility = Sqrt(_factor * sum) * _annualisationFactor;
        }

        if (isFinal)
        {
            _window.TryAdd(squared, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Pv", volatility } };
        }

        return new StreamingIndicatorStateResult(volatility, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The Rogers-Satchell volatility, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateRogersSatchellVolatility</c>. The window holds each bar's
/// own contribution, which reads its high and low against both its open and its close.
/// </remarks>
public sealed class RogersSatchellVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;
    private readonly double _annualisationFactor = Sqrt(252);

    public RogersSatchellVolatilityState(int length = 20)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.RogersSatchellVolatility;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var logHc = value != 0 ? Log(bar.High / value) : 0;
        var logHo = bar.Open != 0 ? Log(bar.High / bar.Open) : 0;
        var logLc = value != 0 ? Log(bar.Low / value) : 0;
        var logLo = bar.Open != 0 ? Log(bar.Low / bar.Open) : 0;
        var term = (logHc * logHo) + (logLc * logLo);

        double volatility = 0;
        if (_window.Count + 1 >= _length)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window: these terms all
            // but cancel on a series that sits outside the bar's range, and a different order decides the
            // sign of what is left, which the root then turns into NaN.
            var start = _window.Count - (_length - 1);
            double sum = 0;
            for (var i = start; i < _window.Count; i++)
            {
                sum += _window[i];
            }

            sum += term;

            volatility = Sqrt(sum / _length) * _annualisationFactor;
        }

        if (isFinal)
        {
            _window.TryAdd(term, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Rsv", volatility } };
        }

        return new StreamingIndicatorStateResult(volatility, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The deviation of the typical price about its own average, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateTypicalPriceVolatility</c>. Quoted in the price's own
/// units rather than annualised.
/// </remarks>
public sealed class TypicalPriceVolatilityState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public TypicalPriceVolatilityState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.TypicalPriceVolatility;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var typicalPrice = (bar.High + bar.Low + value) / 3;

        double volatility = 0;
        if (_window.Count + 1 >= _length)
        {
            // Summed oldest first with this bar last, as the batch engine sums its window.
            var start = _window.Count - (_length - 1);
            double sum = 0;
            for (var i = start; i < _window.Count; i++)
            {
                sum += _window[i];
            }

            sum += typicalPrice;

            var mean = sum / _length;
            double sumSquaredDev = 0;
            for (var i = start; i < _window.Count; i++)
            {
                var dev = _window[i] - mean;
                sumSquaredDev += dev * dev;
            }

            var currentDev = typicalPrice - mean;
            sumSquaredDev += currentDev * currentDev;

            volatility = Sqrt(sumSquaredDev / _length);
        }

        if (isFinal)
        {
            _window.TryAdd(typicalPrice, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Tpv", volatility } };
        }

        return new StreamingIndicatorStateResult(volatility, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}
