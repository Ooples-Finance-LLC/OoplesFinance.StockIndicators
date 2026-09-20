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
    private readonly IReadOnlyList<Bar> _bars;
    private readonly Func<IIndicator, double[][]?> _resolveBuiltIn;

    internal CustomIndicatorEngine(IReadOnlyList<Bar> bars, Func<IIndicator, double[][]?> resolveBuiltIn)
    {
        _bars = bars;
        _resolveBuiltIn = resolveBuiltIn;
    }

    /// <summary>Computes an indicator and everything it depends on, once each.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the graph cannot be computed.</exception>
    internal double[][] Compute(IIndicator indicator)
    {
        if (_computed.TryGetValue(indicator, out var already))
        {
            return already;
        }

        // A built-in was computed by the evaluator, which is the whole point of routing them there: the
        // custom engine never re-implements a calculation the library already has.
        var builtIn = _resolveBuiltIn(indicator);
        if (builtIn is not null)
        {
            _computed[indicator] = builtIn;
            return builtIn;
        }

        var state = indicator switch
        {
            IndicatorBase single => single.CreateState(),
            MultiOutputIndicatorBase multi => multi.CreateState(),
            _ => null
        };

        if (state is null)
        {
            throw new InvalidOperationException(
                indicator.GetType().Name + " supplies no arithmetic and is not a built-in indicator.");
        }

        // Depth first, so a component is ready before the indicator that reads it. Of() already refuses a
        // cycle at the call that would close one, so recursion here terminates.
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

    private static Bar WithClose(in Bar bar, double close) =>
        new(bar.Time, bar.Open, bar.High, bar.Low, close, bar.Volume);
}
