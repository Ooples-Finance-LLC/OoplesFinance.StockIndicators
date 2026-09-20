//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// Where bars come from.
/// </summary>
/// <remarks>
/// <para>
/// Finite for history, open-ended for a live feed, and that is the entire difference between a backtest and a
/// running strategy. Everything downstream - the indicators, the reads, the loop - is the same either way,
/// which is what makes the two programs differ by one line.
/// </para>
/// <para>
/// A caller's own implementation is a first-class source. The shipped adapters have no privileged access.
/// </para>
/// </remarks>
public interface IBarSource
{
    /// <summary>Whether the bars run out, which is what makes a run complete.</summary>
    bool IsFinite { get; }

    /// <summary>The bars, in order.</summary>
    IAsyncEnumerable<Bar> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bars to warm the indicators with before the first published value.
    /// </summary>
    /// <remarks>
    /// A live feed seeded from history returns it here. History itself returns nothing, because it is already
    /// its own warm-up.
    /// </remarks>
    IAsyncEnumerable<Bar> ReadWarmupAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds a <see cref="IBarSource"/> from what a caller already has.
/// </summary>
public static class Bars
{
    /// <summary>
    /// A finite source over any sequence, through a projection.
    /// </summary>
    /// <remarks>
    /// The projection is what keeps this library free of any broker reference: Alpaca's <c>IBar</c>, a CSV
    /// row and an in-house type all arrive the same way, and none of them is named here.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when items or project is null.</exception>
    public static IBarSource From<T>(IEnumerable<T> items, Func<T, Bar> project)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (project is null) throw new ArgumentNullException(nameof(project));

        return new EnumerableBarSource<T>(items, project);
    }

    /// <summary>A finite source over bars the caller already has.</summary>
    /// <exception cref="ArgumentNullException">Thrown when bars is null.</exception>
    public static IBarSource From(IEnumerable<Bar> bars) => From(bars, bar => bar);

    private sealed class EnumerableBarSource<T> : IBarSource
    {
        private readonly IEnumerable<T> _items;
        private readonly Func<T, Bar> _project;

        internal EnumerableBarSource(IEnumerable<T> items, Func<T, Bar> project)
        {
            _items = items;
            _project = project;
        }

        public bool IsFinite => true;

        public async IAsyncEnumerable<Bar> ReadAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _project(item);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async IAsyncEnumerable<Bar> ReadWarmupAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // History warms itself: every bar it has is already fed through in order.
            yield break;
        }
    }
}
