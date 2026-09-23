//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using OoplesFinance.StockIndicators.Builder;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// What a configured set of indicators produced over a source.
/// </summary>
/// <remarks>
/// <para>
/// One type whether the source was finite or live, because batch is the case where the bars run out. There is
/// no string indexer, deliberately: a series is addressed by the indicator object the caller configured, or by
/// one of its typed output members.
/// </para>
/// </remarks>
public interface IIndicatorRun : IAsyncEnumerable<IBarSnapshot>, IDisposable
{
    /// <summary>The series this indicator published.</summary>
    /// <exception cref="ArgumentNullException">Thrown when indicator is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the indicator was not configured on this run.</exception>
    ReadOnlySpan<double> this[IIndicator indicator] { get; }

    /// <summary>One of an indicator's published series.</summary>
    /// <exception cref="ArgumentNullException">Thrown when output is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the output's indicator was not configured.</exception>
    ReadOnlySpan<double> this[IIndicatorOutput output] { get; }

    /// <summary>How many bars were read.</summary>
    int BarCount { get; }

    /// <summary>Whether the source has run out, which a live one never does.</summary>
    bool IsComplete { get; }

    /// <summary>The most recent bar and what every indicator made of it.</summary>
    /// <exception cref="InvalidOperationException">Thrown before any bar has arrived.</exception>
    IBarSnapshot Latest { get; }
}

/// <summary>
/// A run over a finite source, computed in full before it is handed back.
/// </summary>
/// <remarks>
/// Backed by the existing evaluator rather than a second engine. Every value here comes from the same arms
/// and the same batch calculations the v1 surface uses, which is the only way the two can be held to agreeing
/// bar for bar - and a new API that returns different numbers is not a new API, it is a regression with
/// better syntax.
/// </remarks>
internal sealed class IndicatorRun : IIndicatorRun
{
    private readonly Dictionary<IIndicatorOutput, double[]> _series;
    private readonly IndicatorRuntime _runtime;
    private readonly IReadOnlyList<Bar> _bars;

    internal IndicatorRun(IndicatorRuntime runtime, Dictionary<IIndicatorOutput, double[]> series,
        IReadOnlyList<Bar> bars)
    {
        _runtime = runtime;
        _series = series;
        _bars = bars;
        BarCount = bars.Count;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<double> this[IIndicator indicator]
    {
        get
        {
            if (indicator is null) throw new ArgumentNullException(nameof(indicator));

            // A single-output indicator is its own output, which is what lets run[rsi] work without the
            // caller naming one. A multi-output indicator names the series it stands for, because the batch's
            // key order does not always publish that one first: Outputs[0] is the efficiency ratio on Kaufman's
            // adaptive average and the positive directional indicator on the average directional index.
            return this[indicator is IPrimaryOutputIndicator named ? named.PrimaryOutput : indicator.Outputs[0]];
        }
    }

    /// <inheritdoc/>
    public ReadOnlySpan<double> this[IIndicatorOutput output]
    {
        get
        {
            if (output is null) throw new ArgumentNullException(nameof(output));

            if (!_series.TryGetValue(output, out var values))
            {
                throw new KeyNotFoundException(
                    "That series was not configured on this run. Pass the indicator to ConfigureIndicators "
                    + "before reading it.");
            }

            return values;
        }
    }

    /// <inheritdoc/>
    public int BarCount { get; }

    /// <inheritdoc/>
    public bool IsComplete => true;

    /// <inheritdoc/>
    public IBarSnapshot Latest => BarCount > 0
        ? new BarSnapshot(_bars[BarCount - 1], BarCount - 1, _series)
        : throw new InvalidOperationException("No bars have arrived yet.");

    /// <summary>
    /// Every bar in order, so a loop over history reads exactly like a loop over a live feed.
    /// </summary>
    /// <remarks>
    /// A finite run has already computed everything by the time it is handed back, so this replays what it
    /// holds rather than doing work. The loop ends, which is what history means.
    /// </remarks>
    public async IAsyncEnumerator<IBarSnapshot> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < BarCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new BarSnapshot(_bars[i], i, _series);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => _runtime.Dispose();
}
