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
/// Compares indicators by identity.
/// </summary>
/// <remarks>
/// Explicit rather than the BCL's ReferenceEqualityComparer, which is .NET 5+ and is shadowed on the older
/// targets by an inaccessible polyfill - the resulting CS0122 depends on which framework is building, which
/// is a poor thing to leave to chance in a library targeting three.
/// </remarks>
internal sealed class IndicatorIdentity : IEqualityComparer<IIndicator>
{
    internal static readonly IndicatorIdentity Comparer = new();

    public bool Equals(IIndicator? x, IIndicator? y) => ReferenceEquals(x, y);

    public int GetHashCode(IIndicator obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

/// <summary>
/// Drives the arithmetic a caller wrote, over the same bars the built-in indicators saw.
/// </summary>
/// <remarks>
/// <para>
/// A custom indicator supplies only <c>Update</c>, called once per bar in order, and this feeds it. The same
/// state is driven over a finite source and over a live one, which is what makes it impossible for a custom
/// indicator's batch and streaming forms to disagree - there is only one form.
/// </para>
/// <para>
/// Components and chained sources are computed first. An indicator reached through <c>Uses</c> or <c>Of</c> is
/// a node like any other, so it is computed once for the run however many indicators asked for it.
/// </para>
/// </remarks>
internal sealed class CustomIndicatorEngine
{
    private readonly Dictionary<IIndicator, double[][]> _computed = new(IndicatorIdentity.Comparer);

    // Of() refuses a cycle it can see in the Source chain, which is not every cycle: a component reached
    // through Uses(), or a mix of the two, closes a loop Of() never looks at. Compute recurses before it
    // records anything in _computed, so such a graph recurses until the stack ends. This is what is
    // currently being visited, and reaching one of them again is the cycle.
    private readonly HashSet<IIndicator> _visiting = new(IndicatorIdentity.Comparer);
    private readonly IReadOnlyList<Bar> _bars;
    private static readonly BarTimeframe Timeframe = BarTimeframe.Minutes(1);

    private readonly Func<IIndicator, double[][]?> _resolveBuiltIn;

    // A built-in the evaluator could not be asked for - because it reads another indicator's series, or
    // takes a component the evaluator has no handle for - is driven here instead, through the same
    // streaming state the live run drives it with. Component substitution needs no path of its own: a
    // component is another node in this graph, which is what the graph was for.
    private readonly Func<IIndicator, (object? State, IReadOnlyList<string>? Keys)>? _createBuiltInState;

    // Runs a built-in's own calculation with the one average it asks for answered by a caller's series,
    // and reports how many averages it asked for - one means the substitution was unambiguous.
    private readonly Func<IIndicator, Func<IReadOnlyList<double>, int, IReadOnlyList<double>>,
        (double[][]? Values, int Requests)>? _computeWithAverage;

    /// <summary>Runs an indicator over a series of values rather than over the bars' own closes.</summary>
    /// <remarks>
    /// The same rewriting <c>Of()</c> does: the bar keeps its high, low and volume, and its close becomes
    /// the value being smoothed. That is what lets a caller's average apply to a true range or an
    /// oscillator and not only to price.
    /// </remarks>
    internal IReadOnlyList<double> RunOver(IIndicator indicator, IReadOnlyList<double> series)
    {
        var state = indicator switch
        {
            IndicatorBase single => single.CreateState(),
            MultiOutputIndicatorBase multi => multi.CreateState(),
            _ => null
        };

        var values = new double[series.Count];
        if (state is not IIndicatorState simple)
        {
            return values;
        }

        for (var i = 0; i < series.Count && i < _bars.Count; i++)
        {
            values[i] = simple.Update(WithClose(_bars[i], series[i]));
        }

        return values;
    }

    internal CustomIndicatorEngine(IReadOnlyList<Bar> bars, Func<IIndicator, double[][]?> resolveBuiltIn,
        Func<IIndicator, (object? State, IReadOnlyList<string>? Keys)>? createBuiltInState = null,
        Func<IIndicator, Func<IReadOnlyList<double>, int, IReadOnlyList<double>>,
            (double[][]? Values, int Requests)>? computeWithAverage = null)
    {
        _bars = bars;
        _resolveBuiltIn = resolveBuiltIn;
        _createBuiltInState = createBuiltInState;
        _computeWithAverage = computeWithAverage;
    }

    private static OhlcvBar ToOhlcv(in Bar bar) =>
        new("history", Timeframe, bar.Time, bar.Time, bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);

    /// <summary>Computes an indicator and everything it depends on, once each.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the graph cannot be computed.</exception>
    internal double[][] Compute(IIndicator indicator)
    {
        if (_computed.TryGetValue(indicator, out var already))
        {
            return already;
        }

        if (!_visiting.Add(indicator))
        {
            throw new InvalidOperationException(
                indicator.GetType().Name + " depends on itself through its components or its source. "
                + "An indicator cannot be part of what it reads.");
        }

        try
        {

        // A built-in was computed by the evaluator, which is the whole point of routing them there: the
        // custom engine never re-implements a calculation the library already has.
        var builtIn = _resolveBuiltIn(indicator);
        if (builtIn is not null)
        {
            _computed[indicator] = builtIn;
            return builtIn;
        }

        // The indicator's own arithmetic first, even for a built-in. A built-in that was handed a
        // component the options type cannot name supplies a composed state that reads that component, and
        // that is the whole answer; only one with nothing of its own falls through to the streaming state
        // the batch calculation is held to.
        IReadOnlyList<string>? streamingKeys = null;
        var state = indicator switch
        {
            IndicatorBase single => single.CreateState(),
            MultiOutputIndicatorBase multi => multi.CreateState(),
            _ => null
        };

        if (state is null && indicator is IBuiltInIndicator && _createBuiltInState is not null)
        {
            // The streaming state is built from CreateOptions(), which names a smoother by MovingAvgType.
            // A component that is not one of ours has no member there, so it would be dropped and the
            // indicator would answer with its default - the right number for a question nobody asked. 370
            // generated types are in this position: they declare the component correctly and have no
            // arithmetic of their own to consume it yet. Refusing is the only honest answer until they do.
            IIndicator? substitute = null;
            foreach (var component in indicator.Components)
            {
                if (component is not IBuiltInMovingAverage)
                {
                    substitute = component;
                    break;
                }
            }

            if (substitute is not null && _computeWithAverage is not null)
            {
                // The indicator's own calculation, with the single average it asks for answered by the
                // caller's series. Nothing is re-implemented, so the substitution is exact - and if it
                // asked for more than one average, which of them was meant is ambiguous and it refuses
                // rather than swapping the wrong one.
                var (substituted, requests) = _computeWithAverage(indicator,
                    (series, _) => RunOver(substitute, series));
                if (substituted is not null && requests == 1)
                {
                    _computed[indicator] = substituted;
                    return substituted;
                }

                throw new NotSupportedException(
                    indicator.GetType().Name + " cannot take " + substitute.GetType().Name
                    + " as its average: it asks for " + requests + " averages, so which one you meant is "
                    + "ambiguous. Use one of the library's averages here, or write this as a custom "
                    + "indicator of your own.");
            }

            (state, streamingKeys) = _createBuiltInState(indicator);
        }

        if (state is null)
        {
            throw new InvalidOperationException(
                indicator.GetType().Name + " supplies no arithmetic and is not a built-in indicator.");
        }

        // Depth first, so a component is ready before the indicator that reads it. Recursion terminates
        // because _visiting refuses a graph that comes back to something already on the stack.
        var components = new double[indicator.Components.Count][];
        for (var i = 0; i < indicator.Components.Count; i++)
        {
            components[i] = Compute(indicator.Components[i])[0];
        }

        var chained = indicator.Source is null ? null : Compute(indicator.Source)[0];

        var outputCount = indicator.Outputs.Count;
        var results = new double[outputCount][];
        for (var i = 0; i < outputCount; i++)
        {
            results[i] = new double[_bars.Count];
        }

        var componentValues = new double[components.Length];
        var outputValues = new double[outputCount];

        for (var bar = 0; bar < _bars.Count; bar++)
        {
            // A chained indicator reads the series it was given rather than the close, which is what Of()
            // means. The rest of the bar is left alone: an indicator reading highs and lows still gets them.
            var input = chained is null ? _bars[bar] : WithClose(_bars[bar], chained[bar]);

            for (var i = 0; i < components.Length; i++)
            {
                componentValues[i] = components[i][bar];
            }

            switch (state)
            {
                // The same adaptation LiveIndicatorRun makes, so a built-in reached through Of() or given a
                // component computes here exactly what it computes there.
                case IStreamingIndicatorState streaming:
                    {
                        var streamed = streaming.Update(ToOhlcv(input), isFinal: true, includeOutputs: true);
                        results[0][bar] = streamed.Value;

                        if (outputCount > 1 && streamed.Outputs is not null && streamingKeys is not null)
                        {
                            for (var i = 0; i < outputCount && i < streamingKeys.Count; i++)
                            {
                                results[i][bar] = streamed.Outputs.TryGetValue(streamingKeys[i], out var v)
                                    ? v
                                    : streamed.Value;
                            }
                        }

                        break;
                    }

                case IIndicatorState single:
                    results[0][bar] = single.Update(in input);
                    break;

                case IComposedIndicatorState composed:
                    results[0][bar] = composed.Update(in input, componentValues);
                    break;

                case IMultiOutputState multi:
                    Array.Clear(outputValues, 0, outputValues.Length);
                    multi.Update(in input, outputValues);
                    for (var i = 0; i < outputCount; i++)
                    {
                        results[i][bar] = outputValues[i];
                    }

                    break;

                case IComposedMultiOutputState composedMulti:
                    Array.Clear(outputValues, 0, outputValues.Length);
                    composedMulti.Update(in input, componentValues, outputValues);
                    for (var i = 0; i < outputCount; i++)
                    {
                        results[i][bar] = outputValues[i];
                    }

                    break;

                default:
                    throw new InvalidOperationException(
                        indicator.GetType().Name + " returned a " + state.GetType().Name
                        + ", which is not a state this engine can drive.");
            }
        }

        _computed[indicator] = results;
        return results;
        }
        finally
        {
            _visiting.Remove(indicator);
        }
    }

    private static Bar WithClose(in Bar bar, double close) =>
        new(bar.Time, bar.Open, bar.High, bar.Low, close, bar.Volume);
}
