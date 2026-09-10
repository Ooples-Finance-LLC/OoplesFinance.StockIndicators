//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// Base class for an indicator. Derive from this, override <see cref="Calculate"/>, and everything
/// else - resolving the input series, the derived series most indicators need, publishing outputs,
/// and taking part in chaining - is handled here.
/// </summary>
/// <remarks>
/// <para>
/// A complete indicator is the calculation and nothing else:
/// </para>
/// <code>
/// [Indicator("Range Bands")]
/// public sealed class RangeBands : IndicatorBase
/// {
///     public RangeBands(int length = 20, double multiplier = 2)
///     {
///         Length = length;
///         Multiplier = multiplier;
///     }
///
///     public int Length { get; }
///     public double Multiplier { get; }
///
///     protected override void Calculate()
///     {
///         var basis = MovingAverage(Input, MovingAvgType.SimpleMovingAverage, Length);
///         var range = AverageTrueRange(Length);
///
///         var upper = new List&lt;double&gt;(Count);
///         var lower = new List&lt;double&gt;(Count);
///         for (var i = 0; i &lt; Count; i++)
///         {
///             upper.Add(basis[i] + (range[i] * Multiplier));
///             lower.Add(basis[i] - (range[i] * Multiplier));
///         }
///
///         Publish("UpperBand", upper);
///         Publish("MiddleBand", basis);
///         Publish("LowerBand", lower);
///         SetPrimary(basis);
///     }
/// }
/// </code>
/// <para>
/// Running it returns a <see cref="StockData"/>, exactly like the built-in indicators, so it chains
/// both ways with no further work:
/// </para>
/// <code>
/// var bands = new RangeBands(20, 2).Run(data);
/// var rsiOfUpper = bands.SeriesView("UpperBand").CalculateRelativeStrengthIndex(14);
/// var onAnIndicator = new RangeBands().Run(data.CalculateSimpleMovingAverage(50));
/// </code>
/// <para>
/// The input series is resolved once, when the run starts, and cannot change underneath the
/// calculation - the helpers below take it as a parameter rather than reading it back off shared
/// state. That is what makes the class of defect described in issue #145 impossible to write here.
/// </para>
/// </remarks>
public abstract class IndicatorBase
{
    private StockData? _data;
    private IndicatorSource _source;
    private Dictionary<string, List<double>>? _outputs;
    private List<double>? _primary;

    /// <summary>
    /// The name this indicator is known by. Defaults to the type name, or to the name given to
    /// <see cref="IndicatorAttribute"/> when one is applied.
    /// </summary>
    public virtual string Name
    {
        get
        {
            var type = GetType();
            var attribute = (IndicatorAttribute?)Attribute.GetCustomAttribute(type, typeof(IndicatorAttribute));

            return attribute?.Name ?? type.Name;
        }
    }

    /// <summary>
    /// The bars and the series being measured, resolved once at the start of the run.
    /// </summary>
    protected IndicatorSource Source => _source;

    /// <summary>The series this indicator is measuring - close prices, or a chained series.</summary>
    protected IReadOnlyList<double> Input => _source.Values;

    /// <summary>The number of bars.</summary>
    protected int Count => _source.Count;

    /// <summary>
    /// Runs the indicator over <paramref name="data"/> and returns the result.
    /// </summary>
    /// <remarks>
    /// <paramref name="data"/> is not modified. The returned <see cref="StockData"/> is a view over the
    /// same bars carrying this indicator's outputs, so it can be chained from or branched freely.
    /// </remarks>
    /// <param name="data">The bars, or the result of another indicator to chain from.</param>
    public StockData Run(StockData data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        _data = data;
        _source = IndicatorSource.Resolve(data);
        _outputs = new Dictionary<string, List<double>>();
        _primary = null;

        Calculate();

        var outputs = _outputs;
        var primary = _primary;

        if (primary is null && outputs.Count > 0)
        {
            // An indicator that published outputs but named no primary chains from its first one.
            foreach (var series in outputs.Values)
            {
                primary = series;
                break;
            }
        }

        var result = data.WithValues(primary ?? new List<double>());
        result.SetOutputValues(() => outputs);
        result.IndicatorName = IndicatorName.None;

        _data = null;
        _outputs = null;
        _primary = null;

        return result;
    }

    /// <summary>
    /// Computes this indicator. The only thing a new indicator has to write.
    /// </summary>
    protected abstract void Calculate();

    /// <summary>
    /// Publishes a named output, which becomes chainable and appears on the result.
    /// </summary>
    /// <param name="name">The output name, as callers will ask for it.</param>
    /// <param name="values">The series. Must have one value per bar.</param>
    protected void Publish(string name, List<double> values)
    {
        if (name is null || name.Length == 0)
        {
            throw new ArgumentException("An output name is required.", nameof(name));
        }

        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (_outputs is null)
        {
            throw new InvalidOperationException("Publish may only be called while the indicator is running.");
        }

        if (values.Count != Count)
        {
            throw new CalculationException(
                $"{Name} published '{name}' with {values.Count} values for {Count} bars. Every output must "
                + "have one value per bar so that it lines up with the price series.");
        }

        _outputs[name] = values;
    }

    /// <summary>
    /// Names the series a chained calculation continues from when no output is named explicitly.
    /// </summary>
    /// <remarks>Optional. Without it the first published output is used.</remarks>
    protected void SetPrimary(List<double> values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        _primary = values;
    }

    // ---------------------------------------------------------------------------------------------
    // The derived series most indicators need. Each takes the series it measures as a parameter, so
    // computing one cannot change what another reads.
    // ---------------------------------------------------------------------------------------------

    /// <summary>A moving average of <paramref name="values"/>.</summary>
    protected List<double> MovingAverage(IReadOnlyList<double> values, MovingAvgType maType, int length) =>
        IndicatorMath.MovingAverage(RequireData(), values, maType, length);

    /// <summary>A moving average of the input series.</summary>
    protected List<double> MovingAverage(MovingAvgType maType, int length) =>
        MovingAverage(Input, maType, length);

    /// <summary>
    /// The rolling standard deviation of <paramref name="values"/> - the population standard deviation
    /// about each window's own mean, which is what dispersion bands are defined against.
    /// </summary>
    protected List<double> StandardDeviation(IReadOnlyList<double> values, int length) =>
        IndicatorMath.RollingStandardDeviation(values, length);

    /// <summary>The rolling standard deviation of the input series.</summary>
    protected List<double> StandardDeviation(int length) => StandardDeviation(Input, length);

    /// <summary>The true range of each bar.</summary>
    protected List<double> TrueRange() => IndicatorMath.TrueRange(_source);

    /// <summary>The average true range, smoothed by <paramref name="maType"/>.</summary>
    protected List<double> AverageTrueRange(int length,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod) =>
        IndicatorMath.AverageTrueRange(RequireData(), _source, maType, length);

    /// <summary>A new series sized to the bar count, for accumulating results.</summary>
    protected List<double> NewSeries() => new(Count);

    private StockData RequireData() =>
        _data ?? throw new InvalidOperationException(
            "This may only be called while the indicator is running, from inside Calculate.");
}
