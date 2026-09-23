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
/// One series an indicator publishes.
/// </summary>
/// <remarks>
/// <para>
/// There is deliberately no way to construct one of these from a name. An output is reached only through a
/// typed member of the indicator that publishes it - <c>bands.Upper</c>, <c>macd.Histogram</c> - so a caller
/// cannot misspell one, invent one, or address a series the indicator does not publish. A string key can do
/// all three, and every one of them compiles.
/// </para>
/// <para>
/// <see cref="Slot"/> is the position this output occupies in its indicator's <see cref="IIndicator.Outputs"/>,
/// and therefore the index a state writes it to. It is not an identity: two outputs of two indicators share a
/// slot number without being related, which is why lookups take the output itself.
/// </para>
/// </remarks>
public interface IIndicatorOutput
{
    /// <summary>The indicator that publishes this series.</summary>
    IIndicator Indicator { get; }

    /// <summary>Where this series sits among its indicator's outputs, in declaration order.</summary>
    int Slot { get; }
}

/// <summary>
/// Something that produces one or more series from bars.
/// </summary>
/// <remarks>
/// <para>
/// An indicator is identified by being itself. There is no name to match and no convention to learn: a caller
/// holds the object they configured and indexes the result with it. That is why nothing here returns a string,
/// and why two <c>new Sma(20)</c> instances are two indicators rather than a collision.
/// </para>
/// <para>
/// An implementation written outside this library is not a lesser thing. The generated indicators implement
/// exactly this interface, take part in chaining and composition on the same terms, and are computed by the
/// same engine.
/// </para>
/// </remarks>
public interface IIndicator
{
    /// <summary>
    /// The series this indicator reads, or <see langword="null"/> to read the bars themselves.
    /// </summary>
    /// <remarks>
    /// An object rather than a name, so a chain reference cannot dangle or point at something that was never
    /// configured. Set by <c>Of(...)</c>.
    /// </remarks>
    IIndicator? Source { get; }

    /// <summary>
    /// Every series this indicator publishes, in declaration order.
    /// </summary>
    /// <remarks>
    /// A single-output indicator returns one output that is its own value, which is what lets
    /// <c>run[rsi]</c> work without naming an output.
    /// </remarks>
    IReadOnlyList<IIndicatorOutput> Outputs { get; }

    /// <summary>
    /// Other indicators this one is built from, in the order its state receives them.
    /// </summary>
    /// <remarks>
    /// Declared through <c>Uses(...)</c>. A component is a node in the same graph as anything else, so one
    /// component shared by several indicators is computed once, not once per consumer.
    /// </remarks>
    IReadOnlyList<IIndicator> Components { get; }

    /// <summary>
    /// Bars that must be fed in before this indicator's published values mean anything.
    /// </summary>
    /// <remarks>
    /// The builder asks every configured indicator and takes the largest, so a live source knows how much
    /// history to pull before it publishes its first bar. A caller never sets this.
    /// </remarks>
    int WarmupBars { get; }
}

/// <summary>An indicator that publishes more than one series.</summary>
public interface IMultiOutputIndicator : IIndicator
{
}

/// <summary>
/// An indicator that names which of its outputs stands for it.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="IMultiOutputIndicator"/> so that adding it cannot break a type outside this
/// library that already implements that one: an indicator which does not name a primary is resolved
/// through <c>Outputs[0]</c> as before.
/// </para>
/// </remarks>
public interface IPrimaryOutputIndicator
{
    /// <summary>
    /// The series this indicator is named for, used wherever one of its outputs has to stand for it.
    /// </summary>
    /// <remarks>
    /// <see cref="IIndicator.Outputs"/> is in the order the batch publishes its keys, which does not always
    /// put the indicator's own value first - Kaufman's adaptive average publishes its efficiency ratio ahead
    /// of the average, and the average directional index publishes both directional indicators ahead of the
    /// index. Resolving <c>run[indicator]</c> through this rather than through <c>Outputs[0]</c> is what stops
    /// it handing back a diagnostic series in place of the indicator.
    /// </remarks>
    IIndicatorOutput PrimaryOutput { get; }
}


/// <summary>
/// An indicator usable wherever an average of a series is called for.
/// </summary>
/// <remarks>
/// <para>
/// This is what a component parameter asks for instead of a <see cref="MovingAvgType"/>. The enum is a closed
/// list: a caller cannot add to it, and every indicator accepting one has to route through the switch in
/// <c>GetMovingAverageList</c>. An interface takes a caller's own average with no change to this library, and
/// refuses a non-average at compile time, which an enum parameter cannot do.
/// </para>
/// </remarks>
public interface IMovingAverage : IIndicator
{
}

/// <summary>An indicator categorised as <see cref="IndicatorType.Trend"/>.</summary>
public interface ITrendIndicator : IIndicator
{
}

/// <summary>An indicator categorised as <see cref="IndicatorType.Momentum"/>.</summary>
public interface IMomentumIndicator : IIndicator
{
}

/// <summary>An indicator categorised as <see cref="IndicatorType.Volatility"/>.</summary>
public interface IVolatilityIndicator : IIndicator
{
}

/// <summary>An indicator categorised as <see cref="IndicatorType.Volume"/>.</summary>
public interface IVolumeIndicator : IIndicator
{
}

/// <summary>An indicator categorised as <see cref="IndicatorType.Cycle"/>.</summary>
public interface ICycleIndicator : IIndicator
{
}

/// <summary>An indicator categorised as <see cref="IndicatorType.SupportAndResistance"/>.</summary>
public interface ISupportAndResistanceIndicator : IIndicator
{
}
