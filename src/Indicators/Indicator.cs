//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// One series an indicator publishes.
/// </summary>
/// <remarks>
/// Constructed only by <see cref="Indicator"/> and <see cref="MultiOutputIndicator"/>, so the only way a
/// caller obtains one is through a typed member of the indicator that publishes it.
/// </remarks>
internal sealed class IndicatorOutput : IIndicatorOutput
{
    internal IndicatorOutput(IIndicator indicator, int slot)
    {
        Indicator = indicator;
        Slot = slot;
    }

    /// <inheritdoc/>
    public IIndicator Indicator { get; }

    /// <inheritdoc/>
    public int Slot { get; }
}

/// <summary>
/// What a built-in indicator additionally knows: the batch indicator it stands for, and how to build the
/// options the compute layer dispatches on.
/// </summary>
/// <remarks>
/// Internal on purpose, and deliberately NOT part of <see cref="IIndicator"/>. <see cref="IndicatorName"/> is
/// a closed enum of this library's own indicators, so requiring it publicly would make a caller's own
/// indicator impossible to write - which is exactly what <c>IStreamingIndicatorState.Name</c> does today.
/// </remarks>
internal interface IBuiltInIndicator
{
    /// <summary>The batch indicator this stands for.</summary>
    IndicatorName BatchName { get; }

    /// <summary>The single output this stands for, when it stands for one of several.</summary>
    string? BatchOutputKey { get; }

    /// <summary>Builds the options the compute layer dispatches on.</summary>
    IIndicatorSpecOptions CreateOptions();
}

/// <summary>
/// An indicator publishing one series.
/// </summary>
/// <remarks>
/// <para>
/// Derive from this, return a state from <see cref="CreateState"/>, and everything else - taking part in
/// chaining, being someone else's component, having components of its own, running over history and over a
/// live feed - comes from here. The library's own generated indicators derive from this same class on the
/// same terms.
/// </para>
/// </remarks>
public abstract class Indicator : IIndicator
{
    private readonly IIndicatorOutput[] _outputs;
    private IIndicator[] _components = [];

    /// <summary>Creates an indicator publishing a single series, which is the indicator itself.</summary>
    protected Indicator()
    {
        // A single-output indicator is its own output. That is what lets run[rsi] work without naming one,
        // and it is why "no primary declared" is not a mistake this shape can make.
        _outputs = [new IndicatorOutput(this, 0)];
    }

    /// <inheritdoc/>
    public IIndicator? Source { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<IIndicatorOutput> Outputs => _outputs;

    /// <inheritdoc/>
    public IReadOnlyList<IIndicator> Components => _components;

    /// <inheritdoc/>
    /// <remarks>Override when the indicator needs history before its values mean anything.</remarks>
    public virtual int WarmupBars => 0;

    /// <summary>The single series this indicator publishes.</summary>
    public IIndicatorOutput Value => _outputs[0];

    /// <summary>
    /// Reads <paramref name="source"/>'s series instead of the bars.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the chain would read itself.</exception>
    public Indicator Of(IIndicator source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        // Caught here rather than as a stack overflow inside the evaluator, or a cycle error naming handles
        // the caller never saw.
        for (var link = source; link is not null; link = link.Source)
        {
            if (ReferenceEquals(link, this))
            {
                throw new InvalidOperationException(
                    "An indicator cannot read a series that is computed from itself.");
            }
        }

        Source = source;
        return this;
    }

    /// <summary>
    /// Declares the indicators this one is built from, in the order its state receives them.
    /// </summary>
    /// <remarks>
    /// A component is a node in the same graph as anything else, so one component shared by several
    /// indicators is computed once for the run rather than once per consumer.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when components, or any of them, is null.</exception>
    protected void Uses(params IIndicator[] components)
    {
        if (components is null) throw new ArgumentNullException(nameof(components));

        foreach (var component in components)
        {
            if (component is null)
            {
                throw new ArgumentNullException(nameof(components), "A component cannot be null.");
            }
        }

        _components = components;
    }

    /// <summary>
    /// Creates the arithmetic. Return an <see cref="IIndicatorState"/>, or an
    /// <see cref="IComposedIndicatorState"/> when the indicator declared components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called once per run, never shared between runs, so a state is free to hold whatever it needs without
    /// worrying about another run's bars arriving in it.
    /// </para>
    /// <para>
    /// Virtual rather than abstract, and returning <see langword="null"/> by default, because a generated
    /// indicator is not its own arithmetic: it names a batch indicator through <see cref="IBuiltInIndicator"/>
    /// and the compute layer supplies the calculation. Exactly one of the two has to be true of any
    /// indicator, which is what <c>RequireComputable</c> checks.
    /// </para>
    /// </remarks>
    protected internal virtual object? CreateState() => null;
}

/// <summary>
/// An indicator publishing several series.
/// </summary>
public abstract class MultiOutputIndicator : IMultiOutputIndicator
{
    private readonly IIndicatorOutput[] _outputs;
    private IIndicator[] _components = [];

    /// <summary>Creates an indicator publishing <paramref name="outputCount"/> series.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than two outputs are declared.</exception>
    protected MultiOutputIndicator(int outputCount)
    {
        if (outputCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(outputCount),
                "A multi-output indicator publishes at least two series; derive from Indicator for one.");
        }

        _outputs = new IIndicatorOutput[outputCount];
        for (var i = 0; i < outputCount; i++)
        {
            _outputs[i] = new IndicatorOutput(this, i);
        }
    }

    /// <inheritdoc/>
    public IIndicator? Source { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<IIndicatorOutput> Outputs => _outputs;

    /// <inheritdoc/>
    public IReadOnlyList<IIndicator> Components => _components;

    /// <inheritdoc/>
    public virtual int WarmupBars => 0;

    /// <summary>The outputs declared by this indicator, for assigning to its typed members.</summary>
    protected OutputSet DeclaredOutputs => new(_outputs);

    /// <summary>Reads <paramref name="source"/>'s series instead of the bars.</summary>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the chain would read itself.</exception>
    public MultiOutputIndicator Of(IIndicator source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        for (var link = source; link is not null; link = link.Source)
        {
            if (ReferenceEquals(link, this))
            {
                throw new InvalidOperationException(
                    "An indicator cannot read a series that is computed from itself.");
            }
        }

        Source = source;
        return this;
    }

    /// <summary>Declares the indicators this one is built from.</summary>
    /// <exception cref="ArgumentNullException">Thrown when components, or any of them, is null.</exception>
    protected void Uses(params IIndicator[] components)
    {
        if (components is null) throw new ArgumentNullException(nameof(components));

        foreach (var component in components)
        {
            if (component is null)
            {
                throw new ArgumentNullException(nameof(components), "A component cannot be null.");
            }
        }

        _components = components;
    }

    /// <summary>
    /// Creates the arithmetic. Return an <see cref="IMultiOutputState"/>, or an
    /// <see cref="IComposedMultiOutputState"/> when the indicator declared components.
    /// </summary>
    /// <remarks>
    /// Virtual for the same reason as <see cref="Indicator.CreateState"/>: a generated indicator routes to the
    /// compute layer instead of carrying its own arithmetic.
    /// </remarks>
    protected internal virtual object? CreateState() => null;
}

/// <summary>
/// The outputs a multi-output indicator declared, ready to be deconstructed into its typed members.
/// </summary>
/// <remarks>
/// Deconstruction rather than indexing, so <c>(Upper, Middle, Lower) = DeclaredOutputs;</c> fails to compile
/// when the count and the members disagree - a mismatch that an indexer would turn into a runtime surprise
/// somewhere far from the constructor that caused it.
/// </remarks>
public readonly struct OutputSet
{
    private readonly IIndicatorOutput[] _outputs;

    internal OutputSet(IIndicatorOutput[] outputs) => _outputs = outputs;

    /// <summary>Deconstructs two declared outputs.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a different number was declared.</exception>
    public void Deconstruct(out IIndicatorOutput first, out IIndicatorOutput second)
    {
        Require(2);
        first = _outputs[0];
        second = _outputs[1];
    }

    /// <summary>Deconstructs three declared outputs.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a different number was declared.</exception>
    public void Deconstruct(out IIndicatorOutput first, out IIndicatorOutput second, out IIndicatorOutput third)
    {
        Require(3);
        first = _outputs[0];
        second = _outputs[1];
        third = _outputs[2];
    }

    /// <summary>Deconstructs four declared outputs.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a different number was declared.</exception>
    public void Deconstruct(out IIndicatorOutput first, out IIndicatorOutput second, out IIndicatorOutput third,
        out IIndicatorOutput fourth)
    {
        Require(4);
        first = _outputs[0];
        second = _outputs[1];
        third = _outputs[2];
        fourth = _outputs[3];
    }

    /// <summary>Deconstructs five declared outputs.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a different number was declared.</exception>
    public void Deconstruct(out IIndicatorOutput first, out IIndicatorOutput second, out IIndicatorOutput third,
        out IIndicatorOutput fourth, out IIndicatorOutput fifth)
    {
        Require(5);
        first = _outputs[0];
        second = _outputs[1];
        third = _outputs[2];
        fourth = _outputs[3];
        fifth = _outputs[4];
    }

    private void Require(int count)
    {
        var declared = _outputs?.Length ?? 0;
        if (declared != count)
        {
            throw new InvalidOperationException(
                "This indicator declared " + declared + " outputs but is assigning " + count + ".");
        }
    }
}
