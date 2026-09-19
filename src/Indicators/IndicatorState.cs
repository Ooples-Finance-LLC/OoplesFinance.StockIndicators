//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// One bar, as an indicator sees it.
/// </summary>
/// <remarks>
/// <para>
/// A readonly struct rather than <see cref="Streaming.OhlcvBar"/>, which is a class carrying a symbol string
/// and a timeframe object. Those belong to a subscription, not to an indicator's arithmetic, and allocating
/// one per bar to hand an average six doubles is a cost every indicator in a run would pay on every bar.
/// </para>
/// <para>
/// Passed by <see langword="in"/> everywhere, so the struct is never copied into a state's frame.
/// </para>
/// </remarks>
public readonly struct Bar
{
    /// <summary>Creates a bar.</summary>
    public Bar(DateTime time, double open, double high, double low, double close, double volume)
    {
        Time = time;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    /// <summary>When the bar closed.</summary>
    public DateTime Time { get; }

    /// <summary>The opening price.</summary>
    public double Open { get; }

    /// <summary>The highest price traded.</summary>
    public double High { get; }

    /// <summary>The lowest price traded.</summary>
    public double Low { get; }

    /// <summary>The closing price.</summary>
    public double Close { get; }

    /// <summary>The volume traded.</summary>
    public double Volume { get; }
}

/// <summary>
/// The arithmetic of an indicator that publishes one series and is built from nothing else.
/// </summary>
/// <remarks>
/// <para>
/// This is the only thing an indicator's author writes. <see cref="Update"/> is called once per bar, in order,
/// and whatever it returns is that bar's value. There is no separate whole-series method to write, because the
/// library drives this same state over a finite source and over a live one - which is what makes it impossible
/// for an indicator's batch and streaming forms to disagree, the defect class the parity sweeps exist to catch
/// in the library's own indicators.
/// </para>
/// <para>
/// A state sees each bar once and cannot revise a bar it has already answered for. That is not a limitation of
/// this interface so much as the definition of an indicator that can run live: anything needing to look
/// forward rewrites values a consumer has already been given, which is why ZigZag has no streaming form.
/// </para>
/// </remarks>
public interface IIndicatorState
{
    /// <summary>Returns the state to how it was before any bar arrived.</summary>
    void Reset();

    /// <summary>Takes the next bar and returns this bar's value.</summary>
    double Update(in Bar bar);
}

/// <summary>
/// The arithmetic of a single-series indicator built from other indicators.
/// </summary>
/// <remarks>
/// The components arrive already computed for this bar, in the order they were declared to <c>Uses(...)</c>.
/// The library computes each one once for the whole run, so several indicators sharing a component share the
/// work rather than repeating it.
/// </remarks>
public interface IComposedIndicatorState
{
    /// <summary>Returns the state to how it was before any bar arrived.</summary>
    void Reset();

    /// <summary>Takes the next bar and its components' values, and returns this bar's value.</summary>
    double Update(in Bar bar, ReadOnlySpan<double> components);
}

/// <summary>
/// The arithmetic of an indicator that publishes several series.
/// </summary>
/// <remarks>
/// <paramref name="outputs"/> is already the right length - one slot per declared output, in declaration
/// order - so writing past the end is a bounds check rather than a silent overwrite of another indicator's
/// series, which a dictionary keyed by string could not promise.
/// </remarks>
public interface IMultiOutputState
{
    /// <summary>Returns the state to how it was before any bar arrived.</summary>
    void Reset();

    /// <summary>Takes the next bar and fills one value per declared output.</summary>
    void Update(in Bar bar, Span<double> outputs);
}

/// <summary>
/// The arithmetic of a multi-series indicator built from other indicators.
/// </summary>
public interface IComposedMultiOutputState
{
    /// <summary>Returns the state to how it was before any bar arrived.</summary>
    void Reset();

    /// <summary>Takes the next bar and its components' values, and fills one value per declared output.</summary>
    void Update(in Bar bar, ReadOnlySpan<double> components, Span<double> outputs);
}
