using System;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// Implemented by a state whose own default input is something other than the close - a median or
/// typical price, a volume - so that it can be told to read the series a caller supplies instead.
/// </summary>
/// <remarks>
/// <see cref="CustomInputState"/> hands its inner state bars whose close IS the caller's series. A
/// state that reads the close picks that up with no help. A state that reads, say, a median price
/// would compute the median of the adjusted bar instead and silently ignore the series it was given,
/// which is the defect this whole mechanism exists to rule out.
/// </remarks>
internal interface ICustomInputConsumer
{
    /// <summary>Read each bar's close as the input series from now on.</summary>
    void ReadCloseAsInput();
}

/// <summary>
/// Computes any streaming indicator on a series the caller supplies rather than on the bar's close.
/// </summary>
/// <remarks>
/// <para>
/// Callers pass values, not a name for them:
/// <code>
/// var rsi = new CustomInputState(new RelativeStrengthIndexState(14), bar =&gt; (bar.High + bar.Low) / 2);
/// </code>
/// Every state accepts this, so no state can take custom input in one engine and not the other.
/// </para>
/// <para>
/// Each bar, the inner state sees the caller's value as the close, and a high and low decided by the
/// same per-bar rule the batch engine applies to a chained series (see
/// <c>CalculationsHelper.GetCustomRangeLists</c>). A value inside the bar's range keeps the bar's true
/// high and low - a median price, a chained moving average. A value outside it is its own scale, and
/// its high and low are the max and min of the previous and current value. Open and volume are the
/// bar's own, as they are in batch.
/// </para>
/// <para>
/// A preview update (<c>isFinal: false</c>) is measured against the last FINAL value and does not
/// replace it, so revising the forming bar any number of times gives the same answer as seeing it once.
/// </para>
/// </remarks>
public sealed class CustomInputState : IStreamingIndicatorState, IDisposable
{
    private readonly IStreamingIndicatorState _inner;
    private readonly IInputSeries _series;
    private double _prevValue;
    private bool _hasPrev;

    /// <summary>
    /// Wraps <paramref name="inner"/> so that it computes on <paramref name="selector"/>'s values.
    /// </summary>
    /// <param name="inner">
    /// A freshly built state. It is told to read the close if its own default is another series, so
    /// do not also update it directly.
    /// </param>
    /// <param name="selector">The caller's series, one value per bar.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public CustomInputState(IStreamingIndicatorState inner, Func<OhlcvBar, double> selector)
        : this(inner, InputSeries.Of(selector ?? throw new ArgumentNullException(nameof(selector))))
    {
    }

    /// <summary>
    /// Wraps <paramref name="inner"/> so that it computes on <paramref name="series"/>: a preset such as
    /// <see cref="InputSeries.MedianPrice"/>, a windowed series such as <see cref="InputSeries.Midpoint"/>,
    /// or another indicator's output via <see cref="InputSeries.Of(IStreamingIndicatorState)"/>.
    /// </summary>
    /// <param name="inner">
    /// A freshly built state. It is told to read the close if its own default is another series, so
    /// do not also update it directly.
    /// </param>
    /// <param name="series">The input series. One with history must not be shared with another wrapper.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public CustomInputState(IStreamingIndicatorState inner, IInputSeries series)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _series = series ?? throw new ArgumentNullException(nameof(series));

        if (inner is ICustomInputConsumer consumer)
        {
            consumer.ReadCloseAsInput();
        }
    }

    /// <inheritdoc />
    public IndicatorName Name => _inner.Name;

    /// <inheritdoc />
    public void Reset()
    {
        _inner.Reset();
        _series.Reset();
        _prevValue = 0;
        _hasPrev = false;
    }

    /// <inheritdoc />
    public StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs)
    {
        if (bar is null)
        {
            throw new ArgumentNullException(nameof(bar));
        }

        var value = _series.Next(bar, isFinal);

        double high;
        double low;
        if (value >= bar.Low && value <= bar.High)
        {
            high = bar.High;
            low = bar.Low;
        }
        else
        {
            var prev = _hasPrev ? _prevValue : value;
            high = Math.Max(prev, value);
            low = Math.Min(prev, value);
        }

        if (isFinal)
        {
            _prevValue = value;
            _hasPrev = true;
        }

        var custom = new OhlcvBar(bar.Symbol, bar.Timeframe, bar.StartTime, bar.EndTime,
            bar.Open, high, low, value, bar.Volume, bar.IsFinal);

        return _inner.Update(custom, isFinal, includeOutputs);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_inner is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (_series is IDisposable series)
        {
            series.Dispose();
        }
    }
}
