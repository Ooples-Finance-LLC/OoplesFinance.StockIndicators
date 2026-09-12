using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The geometric mean of the window, taken through logarithms, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateGeometricMovingAverage</c>. Each value is floored at a
/// millionth before its logarithm is taken, as the batch engine floors it, and the window is summed oldest
/// first with this bar last so the two agree to the last bit.
/// </remarks>
public sealed class GeometricMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public GeometricMovingAverageState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GeometricMovingAverage;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double gma = 0;
        if (_window.Count + 1 >= _length)
        {
            var start = _window.Count - (_length - 1);
            double logSum = 0;
            for (var i = start; i < _window.Count; i++)
            {
                logSum += Math.Log(Math.Max(_window[i], 0.000001));
            }

            logSum += Math.Log(Math.Max(value, 0.000001));
            gma = Math.Exp(logSum / _length);
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Gma", gma } };
        }

        return new StreamingIndicatorStateResult(gma, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The geometric mean of the window's positive values, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateGeometricMeanMovingAverage</c>. The batch engine walks the
/// window from the newest value back, and this multiplies in that same order, because the product of
/// floating point values depends on the order they are taken in.
/// </remarks>
public sealed class GeometricMeanMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public GeometricMeanMovingAverageState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GeometricMeanMovingAverage;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double gmma;
        if (_window.Count + 1 < _length)
        {
            gmma = value;
        }
        else
        {
            var start = _window.Count - (_length - 1);
            var product = 1.0;
            var used = 0;
            if (value > 0)
            {
                product *= value;
                used++;
            }

            for (var i = _window.Count - 1; i >= start; i--)
            {
                var windowValue = _window[i];
                if (windowValue > 0)
                {
                    product *= windowValue;
                    used++;
                }
            }

            gmma = used > 0 ? Math.Pow(product, 1.0 / used) : 0;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Gmma", gmma } };
        }

        return new StreamingIndicatorStateResult(gmma, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The harmonic mean of the window, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateHarmonicMeanMovingAverage</c>, summing the reciprocals
/// from the newest value back, as the batch engine sums them.
/// </remarks>
public sealed class HarmonicMeanMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public HarmonicMeanMovingAverageState(int length = 14)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.HarmonicMeanMovingAverage;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);

        double hmma;
        if (_window.Count + 1 < _length)
        {
            hmma = value;
        }
        else
        {
            var start = _window.Count - (_length - 1);
            var sum = 0.0;
            var used = 0;
            if (value != 0)
            {
                sum += 1.0 / value;
                used++;
            }

            for (var i = _window.Count - 1; i >= start; i--)
            {
                var windowValue = _window[i];
                if (windowValue != 0)
                {
                    sum += 1.0 / windowValue;
                    used++;
                }
            }

            hmma = used > 0 && sum != 0 ? used / sum : 0;
        }

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Hmma", hmma } };
        }

        return new StreamingIndicatorStateResult(hmma, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}

/// <summary>
/// The gap between a fast and a slow exponential average of the series, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculatePpoMovingAverage</c>. Both averages come from the same
/// <c>EmaState</c> the batch path's exponential average is built on, seeding and all.
/// </remarks>
public sealed class PpoMovingAverageState : IStreamingIndicatorState
{
    private readonly EmaState _fast;
    private readonly EmaState _slow;
    private readonly StreamingInputResolver _input;

    public PpoMovingAverageState(int fastLength = 12, int slowLength = 26)
    {
        _fast = new EmaState(Math.Max(1, fastLength));
        _slow = new EmaState(Math.Max(1, slowLength));
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.PpoMovingAverage;

    public void Reset()
    {
        _fast.Reset();
        _slow.Reset();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var fastEma = _fast.GetNext(value, isFinal);
        var slowEma = _slow.GetNext(value, isFinal);
        var ppoMa = slowEma != 0 ? (fastEma - slowEma) / slowEma * 100 : 0;

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "PpoMa", ppoMa } };
        }

        return new StreamingIndicatorStateResult(ppoMa, outputs);
    }
}

/// <summary>
/// The change in the series over a few bars, in the price's own units, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculatePriceMomentum</c>.
/// </remarks>
public sealed class PriceMomentumState : IStreamingIndicatorState, IDisposable
{
    private readonly int _length;
    private readonly PooledRingBuffer<double> _window;
    private readonly StreamingInputResolver _input;

    public PriceMomentumState(int length = 10)
    {
        _length = Math.Max(1, length);
        _window = new PooledRingBuffer<double>(_length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.PriceMomentum;

    public void Reset()
    {
        _window.Clear();
    }

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _input.GetValue(bar);
        var momentum = _window.Count >= _length ? value - _window[0] : 0;

        if (isFinal)
        {
            _window.TryAdd(value, out _);
        }

        IReadOnlyDictionary<string, double>? outputs = null;
        if (includeOutputs)
        {
            outputs = new Dictionary<string, double>(1) { { "Pm", momentum } };
        }

        return new StreamingIndicatorStateResult(momentum, outputs);
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}
