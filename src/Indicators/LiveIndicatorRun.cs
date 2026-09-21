//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// A run over a source whose bars have not run out.
/// </summary>
/// <remarks>
/// <para>
/// Every indicator is driven one bar at a time, in dependency order, by the same state interfaces a finite
/// run uses for custom indicators. A built-in is driven by its streaming state, which is the same state the
/// library's own parity sweeps hold against its batch calculation.
/// </para>
/// <para>
/// Warm-up bars are fed through before the first published snapshot, so the first live bar does not report an
/// average of one value. They are fed through the same states in the same order, which is what lets a warmed
/// run and a run over the whole history reach the same numbers.
/// </para>
/// </remarks>
internal sealed class LiveIndicatorRun : IIndicatorRun
{
    private static readonly BarTimeframe Timeframe = BarTimeframe.Minutes(1);

    private readonly IBarSource _source;
    private readonly IReadOnlyList<IIndicator> _ordered;
    private readonly Dictionary<IIndicator, object> _states;
    private readonly Dictionary<IIndicator, IReadOnlyList<string>> _outputKeys;
    private readonly Dictionary<IIndicatorOutput, List<double>> _series = [];
    private readonly Dictionary<IIndicator, double[]> _current;
    private Bar _latestBar;
    private int _barCount;
    private bool _isComplete;

    // Inputs seen across warm-up and live bars, against the largest WarmupBars any configured indicator
    // declares. The builder feeds whatever ReadWarmupAsync returns without checking it was enough, so an
    // Sma(14) warmed with four bars was publishing from its first live bar.
    private int _inputsSeen;
    private readonly int _warmupRequired;

    internal LiveIndicatorRun(
        IBarSource source,
        IReadOnlyList<IIndicator> ordered,
        Dictionary<IIndicator, object> states,
        Dictionary<IIndicator, IReadOnlyList<string>> outputKeys,
        IReadOnlyList<IIndicator> configured)
    {
        _source = source;
        _ordered = ordered;
        _states = states;
        _outputKeys = outputKeys;
        _current = new Dictionary<IIndicator, double[]>(IndicatorIdentity.Comparer);

        foreach (var indicator in ordered)
        {
            _current[indicator] = new double[indicator.Outputs.Count];
            if (indicator.WarmupBars > _warmupRequired)
            {
                _warmupRequired = indicator.WarmupBars;
            }
        }

        foreach (var indicator in configured)
        {
            foreach (var output in indicator.Outputs)
            {
                _series[output] = [];
            }
        }
    }

    /// <inheritdoc/>
    public ReadOnlySpan<double> this[IIndicator indicator]
    {
        get
        {
            if (indicator is null) throw new ArgumentNullException(nameof(indicator));
            return this[indicator.Outputs[0]];
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

            return SpanCompat.AsReadOnlySpan(values);
        }
    }

    /// <inheritdoc/>
    public int BarCount => _barCount;

    /// <inheritdoc/>
    /// <remarks>Never true: this run exists because the source does not run out.</remarks>
    // A live source ends when its producer says so - LiveBarSource.Complete drains what is pending and
    // then ends ReadAsync. Hard-coding false meant a caller could never tell a finished feed from a quiet
    // one, so it is set when the enumeration runs to completion rather than being abandoned.
    public bool IsComplete => _isComplete;

    /// <summary>
    /// Whether bars computed over less than <see cref="IIndicator.WarmupBars"/> inputs are published.
    /// </summary>
    /// <remarks>
    /// False by default: anything a caller receives has settled. Set it to see every bar from the first,
    /// with <see cref="IBarSnapshot.IsWarmedUp"/> saying which are worth acting on.
    /// </remarks>
    internal bool PublishBeforeWarmup { get; set; }

    /// <inheritdoc/>
    public IBarSnapshot Latest => _barCount > 0
        ? Snapshot(_latestBar, _barCount - 1, _inputsSeen >= _warmupRequired)
        : throw new InvalidOperationException("No bars have arrived yet.");

    /// <summary>Feeds the warm-up bars through without publishing a snapshot for any of them.</summary>
    internal async Task WarmAsync(CancellationToken cancellationToken)
    {
        await foreach (var bar in _source.ReadWarmupAsync(cancellationToken).ConfigureAwait(false))
        {
            Advance(bar, record: false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerator<IBarSnapshot> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
    {
        await foreach (var bar in _source.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            Advance(bar, record: true);

            // A bar computed over too little history is arithmetic, not information. It is suppressed
            // unless the caller asked to see it, and when it is shown IsWarmedUp says what it is.
            var warmedUp = _inputsSeen >= _warmupRequired;
            if (warmedUp || PublishBeforeWarmup)
            {
                yield return Snapshot(bar, _barCount - 1, warmedUp);
            }
        }

        // Only on a normal end. A caller who stops enumerating early, or cancels, leaves the source live and
        // never reaches this line.
        _isComplete = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var state in _states.Values)
        {
            if (state is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private IBarSnapshot Snapshot(Bar bar, int index, bool warmedUp)
    {
        // This bar's value from each series, not a copy of every series up to it. The copy allocated the
        // whole run again on every bar, so a feed that does not end went quadratic and never stopped.
        var values = new Dictionary<IIndicatorOutput, double>(_series.Count);
        foreach (var pair in _series)
        {
            values[pair.Key] = pair.Value.Count > index ? pair.Value[index] : double.NaN;
        }

        return new BarSnapshot(bar, index, values, warmedUp);
    }

    /// <summary>Drives every indicator one bar, in dependency order.</summary>
    private void Advance(Bar bar, bool record)
    {
        _latestBar = bar;
        _inputsSeen++;

        foreach (var indicator in _ordered)
        {
            // A chained indicator reads the series it was given rather than the close, which is what Of()
            // means - and its source has already been advanced, because the order is dependency first.
            var input = indicator.Source is null
                ? bar
                : new Bar(bar.Time, bar.Open, bar.High, bar.Low, _current[indicator.Source][0], bar.Volume);

            var slots = _current[indicator];

            switch (_states[indicator])
            {
                case IStreamingIndicatorState streaming:
                    {
                        var result = streaming.Update(ToOhlcv(input), isFinal: true, includeOutputs: true);
                        slots[0] = result.Value;

                        if (slots.Length > 1 && result.Outputs is not null
                            && _outputKeys.TryGetValue(indicator, out var keys))
                        {
                            for (var i = 0; i < slots.Length && i < keys.Count; i++)
                            {
                                slots[i] = result.Outputs.TryGetValue(keys[i], out var value)
                                    ? value
                                    : result.Value;
                            }
                        }

                        break;
                    }

                case IIndicatorState single:
                    slots[0] = single.Update(in input);
                    break;

                case IComposedIndicatorState composed:
                    slots[0] = composed.Update(in input, ComponentValues(indicator));
                    break;

                case IMultiOutputState multi:
                    Array.Clear(slots, 0, slots.Length);
                    multi.Update(in input, slots);
                    break;

                case IComposedMultiOutputState composedMulti:
                    Array.Clear(slots, 0, slots.Length);
                    composedMulti.Update(in input, ComponentValues(indicator), slots);
                    break;

                default:
                    throw new InvalidOperationException(
                        indicator.GetType().Name + " has no state this run can drive.");
            }

            if (!record)
            {
                continue;
            }

            for (var i = 0; i < indicator.Outputs.Count; i++)
            {
                if (_series.TryGetValue(indicator.Outputs[i], out var values))
                {
                    values.Add(slots[i]);
                }
            }
        }

        if (record)
        {
            _barCount++;
        }
    }

    private double[] ComponentValues(IIndicator indicator)
    {
        var values = new double[indicator.Components.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = _current[indicator.Components[i]][0];
        }

        return values;
    }

    private static OhlcvBar ToOhlcv(in Bar bar) =>
        new("live", Timeframe, bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
}
