using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// The correctly rounded geometric mean of the clamped window, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateGeometricMovingAverage</c>. Each value is floored at a
/// millionth. Exact products and one final root rounding preserve finite means at extreme magnitudes.
/// </remarks>
[PrimaryOutput("Gma")]
public sealed class GeometricMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingGeometricMean _mean;
    private readonly StreamingInputResolver _input;

    public GeometricMovingAverageState(int length = 14)
    {
        _mean = new RollingGeometricMean(length);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GeometricMovingAverage;
    public void Reset() => _mean.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _mean.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Gma", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _mean.Dispose();
}

/// <summary>
/// The geometric mean of the window's positive values, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateGeometricMeanMovingAverage</c>. Products are exact;
/// nonpositive observations are omitted, and startup copies the source value until the window fills.
/// </remarks>
[PrimaryOutput("Gmma")]
public sealed class GeometricMeanMovingAverageState : IStreamingIndicatorState, IDisposable
{
    private readonly RollingGeometricMean _mean;
    private readonly StreamingInputResolver _input;

    public GeometricMeanMovingAverageState(int length = 14)
    {
        _mean = new RollingGeometricMean(length, positiveOnly: true);
        _input = new StreamingInputResolver(InputName.Close, null);
    }

    public IndicatorName Name => IndicatorName.GeometricMeanMovingAverage;
    public void Reset() => _mean.Reset();

    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        var value = _mean.Next(_input.GetValue(bar), isFinal);
        IReadOnlyDictionary<string, double>? outputs = includeOutputs
            ? new Dictionary<string, double>(1) { { "Gmma", value } } : null;
        return new StreamingIndicatorStateResult(value, outputs);
    }

    public void Dispose() => _mean.Dispose();
}

/// <summary>
/// The harmonic mean of the window, bar by bar.
/// </summary>
/// <remarks>
/// The streaming twin of <c>Calculations.CalculateHarmonicMeanMovingAverage</c>, summing the reciprocals
/// from the newest value back, as the batch engine sums them.
/// </remarks>
[PrimaryOutput("Hmma")]
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
            var sum = new ExactReciprocalSum();
            sum.Add(value);
            for (var i = _window.Count - 1; i >= start; i--) sum.Add(_window[i]);
            hmma = sum.Mean;
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
[PrimaryOutput("PpoMa")]
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
[PrimaryOutput("Pm")]
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
