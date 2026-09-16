using System;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>
/// A series of input values an indicator computes on, supplied one bar at a time.
/// </summary>
/// <remarks>
/// <para>
/// Callers pass values, not a name for them. Most series are a plain function of the bar - a median
/// price, a typical price - and <see cref="InputSeries"/> has one for each of the names callers used
/// to pass. Some need history: a midpoint over 14 bars, or another indicator's output. Those are why
/// this is an interface rather than a <see cref="Func{T, TResult}"/>: <see cref="Next"/> is told
/// whether the bar is forming or final, and a forming bar must not advance any window. A plain
/// function cannot tell the two apart, so a windowed series built on one would count every revision
/// of a forming bar as a new bar.
/// </para>
/// <para>
/// A series with history belongs to one consumer. Build a fresh one for each indicator rather than
/// sharing an instance, or each consumer advances the other's window.
/// </para>
/// </remarks>
public interface IInputSeries
{
    /// <summary>The value for this bar.</summary>
    /// <param name="bar">The bar.</param>
    /// <param name="isFinal">False for a forming bar, which must not advance any window.</param>
    double Next(OhlcvBar bar, bool isFinal);

    /// <summary>Forgets all history.</summary>
    void Reset();
}

/// <summary>
/// Ready-made input series, named after the input names they replace, plus ways to build your own.
/// </summary>
/// <remarks>
/// Code that used to pass an input name - <c>InputName.MedianPrice</c> - passes the preset of the same
/// name instead: <c>InputSeries.MedianPrice</c>. Each preset gives exactly the value that input name
/// selected.
/// <code>
/// var rsi = new CustomInputState(new RelativeStrengthIndexState(14), InputSeries.MedianPrice);
/// var sma = new CustomInputState(new SimpleMovingAverageState(20), InputSeries.Of(new RelativeStrengthIndexState(14)));
/// </code>
/// </remarks>
public static class InputSeries
{
    /// <summary>The close.</summary>
    public static IInputSeries Close { get; } = new BarSeries(bar => bar.Close);

    /// <summary>
    /// The close. Bars carry no separate adjusted price, so this is the close, as the input name it
    /// replaces always was.
    /// </summary>
    public static IInputSeries AdjustedClose { get; } = new BarSeries(bar => bar.Close);

    /// <summary>The open.</summary>
    public static IInputSeries Open { get; } = new BarSeries(bar => bar.Open);

    /// <summary>The high.</summary>
    public static IInputSeries High { get; } = new BarSeries(bar => bar.High);

    /// <summary>The low.</summary>
    public static IInputSeries Low { get; } = new BarSeries(bar => bar.Low);

    /// <summary>The volume.</summary>
    public static IInputSeries Volume { get; } = new BarSeries(bar => bar.Volume);

    /// <summary>(high + low) / 2.</summary>
    public static IInputSeries MedianPrice { get; } = new BarSeries(bar => (bar.High + bar.Low) / 2);

    /// <summary>(high + low + close) / 3.</summary>
    public static IInputSeries TypicalPrice { get; } = new BarSeries(bar => (bar.High + bar.Low + bar.Close) / 3);

    /// <summary>(open + high + low + close) / 4.</summary>
    public static IInputSeries FullTypicalPrice { get; } =
        new BarSeries(bar => (bar.Open + bar.High + bar.Low + bar.Close) / 4);

    /// <summary>(high + low + 2 * close) / 4.</summary>
    public static IInputSeries WeightedClose { get; } =
        new BarSeries(bar => (bar.High + bar.Low + (bar.Close * 2)) / 4);

    /// <summary>(open + close) / 2.</summary>
    public static IInputSeries AveragePrice { get; } = new BarSeries(bar => (bar.Open + bar.Close) / 2);

    /// <summary>The midpoint of the close over <paramref name="length"/> bars. A new series each call.</summary>
    public static IInputSeries Midpoint(int length = 14) => new StateSeries(new MidpointState(length));

    /// <summary>The midprice over <paramref name="length"/> bars. A new series each call.</summary>
    public static IInputSeries Midprice(int length = 14) => new StateSeries(new MidpriceState(length));

    /// <summary>Any function of the bar.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
    public static IInputSeries Of(Func<OhlcvBar, double> selector) =>
        new BarSeries(selector ?? throw new ArgumentNullException(nameof(selector)));

    /// <summary>
    /// Another indicator's output - streaming chaining, the counterpart of <c>data.CalculateX().CalculateY()</c>.
    /// </summary>
    /// <param name="source">A freshly built state. Its primary value is the input, bar by bar.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static IInputSeries Of(IStreamingIndicatorState source) =>
        new StateSeries(source ?? throw new ArgumentNullException(nameof(source)));

    private sealed class BarSeries : IInputSeries
    {
        private readonly Func<OhlcvBar, double> _selector;

        public BarSeries(Func<OhlcvBar, double> selector) => _selector = selector;

        public double Next(OhlcvBar bar, bool isFinal) => _selector(bar);

        public void Reset()
        {
            // Nothing to forget: a function of the bar has no history.
        }
    }

    private sealed class StateSeries : IInputSeries, IDisposable
    {
        private readonly IStreamingIndicatorState _state;

        public StateSeries(IStreamingIndicatorState state) => _state = state;

        public double Next(OhlcvBar bar, bool isFinal) =>
            _state.Update(bar, isFinal, includeOutputs: false).Value;

        public void Reset() => _state.Reset();

        public void Dispose()
        {
            if (_state is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
