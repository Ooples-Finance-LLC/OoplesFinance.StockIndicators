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
/// A source bars are pushed into.
/// </summary>
/// <remarks>
/// <para>
/// The adapter every live feed ends up needing: a broker raises an event, something turns it into a
/// <see cref="Bar"/> and calls <see cref="Publish"/>. The queue is unbounded, so a slow consumer slows the
/// reader rather than losing a bar, and a fast producer never blocks the socket the bar arrived on.
/// </para>
/// </remarks>
public sealed class LiveBarSource : IBarSource
{
    // A queue and a semaphore rather than System.Threading.Channels, which is not part of the framework on
    // net461 and would mean a package reference for one type. Unbounded, so a slow consumer slows the reader
    // rather than losing a bar, and Publish never blocks the socket the bar arrived on.
    private readonly Queue<Bar> _pending = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly object _gate = new();
    private readonly List<Bar> _warmup = [];
    private bool _completed;

    /// <inheritdoc/>
    /// <remarks>Always false. A live feed is the case where the bars do not run out.</remarks>
    public bool IsFinite => false;

    /// <summary>Hands the run a bar.</summary>
    /// <returns>Whether the bar was accepted, which is false once the source has been completed.</returns>
    public bool Publish(Bar bar)
    {
        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }

            _pending.Enqueue(bar);
        }

        _available.Release();
        return true;
    }

    /// <summary>Says no more bars are coming, which ends a loop over this source.</summary>
    public void Complete()
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
        }

        // Wakes a reader that is waiting, so the loop ends rather than hanging on a source that is finished.
        _available.Release();
    }

    /// <summary>
    /// Bars to warm the indicators with before the feed's first bar is published.
    /// </summary>
    /// <remarks>
    /// Seeding from history is what stops the first live bar publishing an average of one value. The bars are
    /// fed through the same states in the same order, so a warmed run and a run over the whole history reach
    /// the same numbers.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when bars is null.</exception>
    public LiveBarSource WarmedWith(IEnumerable<Bar> bars)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));

        _warmup.AddRange(bars);
        return this;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Bar> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            await _available.WaitAsync(cancellationToken).ConfigureAwait(false);

            Bar bar;
            lock (_gate)
            {
                if (_pending.Count == 0)
                {
                    // Nothing queued and the release came from Complete, so the bars have run out.
                    if (_completed)
                    {
                        yield break;
                    }

                    continue;
                }

                bar = _pending.Dequeue();
            }

            yield return bar;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Bar> ReadWarmupAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var bar in _warmup)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return bar;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
