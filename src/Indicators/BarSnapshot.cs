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
/// One bar, and what every configured indicator made of it.
/// </summary>
/// <remarks>
/// The same indexers as <see cref="IIndicatorRun"/>, so the body of a loop over a live feed reads exactly
/// like the body of a loop over history. That is the point of the shape: a strategy is one program.
/// </remarks>
public interface IBarSnapshot
{
    /// <summary>The bar itself.</summary>
    Bar Bar { get; }

    /// <summary>This bar's value for an indicator.</summary>
    /// <exception cref="ArgumentNullException">Thrown when indicator is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the indicator was not configured.</exception>
    double this[IIndicator indicator] { get; }

    /// <summary>This bar's value for one of an indicator's series.</summary>
    /// <exception cref="ArgumentNullException">Thrown when output is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the output's indicator was not configured.</exception>
    double this[IIndicatorOutput output] { get; }

    /// <summary>Where this bar sits in the run, counting from zero.</summary>
    int Index { get; }
}

/// <summary>One bar of a run, reading from series the run already holds.</summary>
internal sealed class BarSnapshot : IBarSnapshot
{
    private readonly IReadOnlyDictionary<IIndicatorOutput, double[]> _series;

    internal BarSnapshot(Bar bar, int index, IReadOnlyDictionary<IIndicatorOutput, double[]> series)
    {
        Bar = bar;
        Index = index;
        _series = series;
    }

    /// <inheritdoc/>
    public Bar Bar { get; }

    /// <inheritdoc/>
    public int Index { get; }

    /// <inheritdoc/>
    public double this[IIndicator indicator]
    {
        get
        {
            if (indicator is null) throw new ArgumentNullException(nameof(indicator));
            return this[indicator.Outputs[0]];
        }
    }

    /// <inheritdoc/>
    public double this[IIndicatorOutput output]
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

            return Index < values.Length ? values[Index] : double.NaN;
        }
    }
}
