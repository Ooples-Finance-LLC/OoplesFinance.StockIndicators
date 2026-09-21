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
/// <see cref="Bar"/> and calls <see cref="Publish"/>. The queue is bounded, so a producer that outruns its
/// consumer waits rather than growing the queue until the process runs out of memory. No bar is ever
/// dropped. A producer that cannot block - a socket callback - calls <see cref="PublishAsync"/> instead.
/// </para>
/// </remarks>
public sealed class LiveBarSource : IBarSource
{
    /// <summary>Queued bars a producer may run ahead by before it is made to wait.</summary>
    public const int DefaultCapacity = 10_000;

    // A queue and two semaphores rather than System.Threading.Channels, which is not part of the framework
    // on net461 and would mean a package reference for one type. _available counts bars a reader can take;
    // _room counts space a producer can fill. The queue was unbounded, which is not a policy - a producer
    // faster than its consumer grew it until the process ran out of memory. Bounding it and making the
    // producer wait is the only policy that never loses a bar, which is what a bar feed needs.
    private readonly Queue<Bar> _pending = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly SemaphoreSlim _room;
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly List<Bar> _warmup = [];
    private bool _completed;

    /// <summary>Creates a source whose producer waits once <paramref name="capacity"/> bars are queued.</summary>
    /// <param name="capacity">Bars the producer may run ahead by. Defaults to <see cref="DefaultCapacity"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is not positive.</exception>
    public LiveBarSource(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                "A source has to hold at least one bar.");
        }

        _capacity = capacity;
        _room = new SemaphoreSlim(capacity, capacity);
    }

    /// <summary>Bars the producer may run ahead by before <see cref="Publish"/> waits.</summary>
    public int Capacity => _capacity;

    /// <summary>Bars queued and not yet read.</summary>
    public int PendingCount
    {
        get { lock (_gate) { return _pending.Count; } }
    }

    /// <inheritdoc/>
    /// <remarks>Always false. A live feed is the case where the bars do not run out.</remarks>
    public bool IsFinite => false;

    /// <summary>Hands the run a bar.</summary>
    /// <returns>Whether the bar was accepted, which is false once the source has been completed.</returns>
    /// <remarks>
    /// Waits while the queue is full. That is the backpressure: a producer on a thread it can afford to
    /// block calls this, and one that cannot - a socket callback - calls <see cref="PublishAsync"/>.
    /// </remarks>
    public bool Publish(Bar bar)
    {
        _room.Wait();
        return Enqueue(bar);
    }

    /// <summary>Hands the run a bar, waiting for room without blocking the caller's thread.</summary>
    /// <returns>Whether the bar was accepted, which is false once the source has been completed.</returns>
    public async Task<bool> PublishAsync(Bar bar, CancellationToken cancellationToken = default)
    {
        await _room.WaitAsync(cancellationToken).ConfigureAwait(false);
        return Enqueue(bar);
    }

    private bool Enqueue(Bar bar)
    {
        lock (_gate)
        {
            if (_completed)
            {
                // The room this bar would have taken goes back, so a producer still publishing into a
                // completed source is not slowly starved of a queue nobody will read.
                _room.Release();
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

            // Freeing the slot before the bar is handed over, so a producer can refill while the consumer
            // works rather than only after it finishes.
            _room.Release();
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
