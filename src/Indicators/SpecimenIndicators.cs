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
/// What a built-in moving average additionally knows: which <see cref="MovingAvgType"/> it is.
/// </summary>
/// <remarks>
/// <para>
/// An indicator taking a component asks for <see cref="IMovingAverage"/>, which any average satisfies - ours
/// or a caller's. When the one it was handed happens to be one of ours, the compute layer can collapse it into
/// the <see cref="MovingAvgType"/> the existing batch calculation already accepts, instead of running it as a
/// separate node. A caller's own average has no enum member and is run as a component, which is the whole
/// point of taking the interface rather than the enum.
/// </para>
/// </remarks>
internal interface IBuiltInMovingAverage
{
    /// <summary>The enum member the existing batch calculations know this average by.</summary>
    MovingAvgType AvgType { get; }
}

/// <summary>
/// Checks that an indicator can actually be computed.
/// </summary>
internal static class IndicatorContract
{
    internal static double NativePrimary(IIndicator indicator, Streaming.StreamingIndicatorStateResult result)
    {
        if (indicator.Outputs.Count == 1 && indicator is IBuiltInIndicator builtIn && builtIn.BatchOutputKey is string key)
        {
            if (result.Outputs is not null && result.Outputs.TryGetValue(key, out var value)) return value;
            throw new InvalidOperationException(indicator.GetType().Name + " native state did not publish its declared output " + key + ".");
        }
        return result.Value;
    }

    internal static IIndicatorOutput PrimaryOutput(IIndicator indicator)
    {
        var output = indicator is IPrimaryOutputIndicator named ? named.PrimaryOutput : indicator.Outputs[0];
        if (output is null || !ReferenceEquals(output.Indicator, indicator) || output.Slot < 0 || output.Slot >= indicator.Outputs.Count)
            throw new InvalidOperationException(indicator.GetType().Name + " must name one of its own outputs as primary.");
        return indicator.Outputs[output.Slot];
    }

    /// <summary>
    /// Every indicator is either its own arithmetic or names a batch indicator. Neither is a mistake that can
    /// be caught later usefully: the run would simply have nothing to compute.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when indicator is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the indicator supplies neither.</exception>
    internal static void RequireComputable(IIndicator indicator)
    {
        if (indicator is null) throw new ArgumentNullException(nameof(indicator));

        if (indicator is IBuiltInIndicator)
        {
            return;
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
                indicator.GetType().Name + " supplies no arithmetic: override CreateState to return a state.");
        }

        // The state exists only to be looked at. A caller's CreateState may hold a pooled buffer or a
        // handle, and nothing else will ever own this one, so it is released here - including on the throw.
        try
        {
            var expected = indicator is IMultiOutputIndicator
                ? state is IMultiOutputState or IComposedMultiOutputState
                : state is IIndicatorState or IComposedIndicatorState;

            if (!expected)
            {
                throw new InvalidOperationException(
                    indicator.GetType().Name + " returned a " + state.GetType().Name
                    + ", which does not match the number of series it publishes.");
            }
        }
        finally
        {
            (state as IDisposable)?.Dispose();
        }
    }
}

/// <summary>Simple moving average.</summary>
/// <remarks>
/// Hand-written specimen of what the generator emits for every options type, kept as the shape the generated
/// output is held to. It is the whole contract in one file: the parameters come from the options type's widest
/// constructor, the category interface from the indicator's <c>[Category]</c> attribute, and the batch
/// indicator and options from <c>BuilderArmTargets</c>.
/// </remarks>
public sealed class Sma : IndicatorBase, IMovingAverage, ITrendIndicator, IBuiltInIndicator, IBuiltInMovingAverage
{
    /// <summary>Creates a simple moving average.</summary>
    public Sma(int length = 14) => Length = length;

    /// <summary>The lookback length.</summary>
    public int Length { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Length;

    MovingAvgType IBuiltInMovingAverage.AvgType => MovingAvgType.SimpleMovingAverage;

    IndicatorName IBuiltInIndicator.BatchName => IndicatorName.SimpleMovingAverage;

    string? IBuiltInIndicator.BatchOutputKey => null;

    IIndicatorSpecOptions IBuiltInIndicator.CreateOptions() => new SmaSpecOptions(Length);
}

/// <summary>Exponential moving average.</summary>
public sealed class Ema : IndicatorBase, IMovingAverage, ITrendIndicator, IBuiltInIndicator, IBuiltInMovingAverage
{
    /// <summary>Creates an exponential moving average.</summary>
    public Ema(int length = 14) => Length = length;

    /// <summary>The lookback length.</summary>
    public int Length { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Length;

    MovingAvgType IBuiltInMovingAverage.AvgType => MovingAvgType.ExponentialMovingAverage;

    IndicatorName IBuiltInIndicator.BatchName => IndicatorName.ExponentialMovingAverage;

    string? IBuiltInIndicator.BatchOutputKey => null;

    IIndicatorSpecOptions IBuiltInIndicator.CreateOptions() => new EmaSpecOptions(Length);
}

/// <summary>Bollinger bands.</summary>
/// <remarks>
/// The specimen for a multi-output indicator that takes a component. Its average is an
/// <see cref="IMovingAverage"/> rather than a <see cref="MovingAvgType"/>, so a caller's own average drops in
/// and a non-average does not compile.
/// </remarks>
public sealed class BollingerBands : MultiOutputIndicatorBase, IVolatilityIndicator, IBuiltInIndicator
{
    private readonly IMovingAverage _average;

    /// <summary>Creates Bollinger bands.</summary>
    /// <param name="length">The lookback length.</param>
    /// <param name="stdDev">How many standard deviations the bands sit from the middle.</param>
    /// <param name="average">The middle band, or <see langword="null"/> for a simple moving average.</param>
    public BollingerBands(int length = 20, double stdDev = 2, IMovingAverage? average = null)
        : base(3)
    {
        Length = length;
        StdDev = stdDev;
        _average = average ?? new Sma(length);

        // Declared as a component so a caller's own average becomes a node in the graph. When it is one of
        // ours, the compute layer collapses it into the MovingAvgType the batch calculation already takes.
        Uses(_average);

        (Upper, Middle, Lower) = DeclaredOutputs;
    }

    /// <summary>The lookback length.</summary>
    public int Length { get; }

    /// <summary>How many standard deviations the bands sit from the middle.</summary>
    public double StdDev { get; }

    /// <summary>The upper band.</summary>
    public IIndicatorOutput Upper { get; }

    /// <summary>The middle band.</summary>
    public IIndicatorOutput Middle { get; }

    /// <summary>The lower band.</summary>
    public IIndicatorOutput Lower { get; }

    /// <summary>The series BollingerBands publishes.</summary>
    /// <remarks>
    /// Per indicator rather than one enum for the library: the containing type is what makes a member
    /// unique, so the names stay short and the compiler knows which keys belong here. Each value is its slot.
    /// </remarks>
    public enum Output
    {
        /// <summary>The upper band.</summary>
        Upper = 0,

        /// <summary>The middle band.</summary>
        Middle = 1,

        /// <summary>The lower band.</summary>
        Lower = 2,
    }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Length, _average.WarmupBars);

    IndicatorName IBuiltInIndicator.BatchName => IndicatorName.BollingerBands;

    string? IBuiltInIndicator.BatchOutputKey => null;

    IIndicatorSpecOptions IBuiltInIndicator.CreateOptions() =>
        // A built-in average collapses to the enum the existing options type takes. A caller's own average
        // cannot, and is run as the declared component instead.
        _average is IBuiltInMovingAverage builtIn
            ? new BollingerBandsSpecOptions(Length, StdDev, builtIn.AvgType)
            : new BollingerBandsSpecOptions(Length, StdDev);

    /// <inheritdoc/>
    /// <remarks>
    /// Null when the average is one of ours, so the batch calculation answers and stays bit for bit what it
    /// always was. When it is the caller's own, the options type has no way to name it - a MovingAvgType
    /// cannot spell a type the library has never heard of - so the bands are computed here instead, reading
    /// the middle line from the component the graph already ran.
    /// </remarks>
    protected internal override object? CreateState() =>
        _average is IBuiltInMovingAverage ? null : new Bands(Length, StdDev);

    /// <summary>Bollinger bands around a middle line somebody else computed.</summary>
    private sealed class Bands : IComposedMultiOutputState
    {
        private readonly int _length;
        private readonly double _stdDev;
        private readonly Queue<double> _window;

        internal Bands(int length, double stdDev)
        {
            _length = Math.Max(1, length);
            _stdDev = stdDev;
            _window = new Queue<double>(_length);
        }

        public void Reset() => _window.Clear();

        public void Update(in Bar bar, ReadOnlySpan<double> components, Span<double> outputs)
        {
            _window.Enqueue(bar.Close);
            if (_window.Count > _length)
            {
                _window.Dequeue();
            }

            // The middle line is the component's value for this bar - whatever the caller handed us - and
            // the width is the deviation of the closes about their own mean, which is what a Bollinger band
            // measures whatever sits in the middle.
            var middle = components.Length > 0 ? components[0] : 0;

            // Nothing until the window is full, which is what the batch calculation publishes. A band drawn
            // from a partial window is a different number from the one the library has always given.
            if (_window.Count < _length)
            {
                outputs[0] = 0;
                outputs[1] = 0;
                outputs[2] = 0;
                return;
            }

            double sum = 0;
            foreach (var close in _window)
            {
                sum += close;
            }

            var mean = sum / _window.Count;

            double variance = 0;
            foreach (var close in _window)
            {
                var deviation = close - mean;
                variance += deviation * deviation;
            }

            var width = _stdDev * Math.Sqrt(variance / _window.Count);

            outputs[0] = middle + width;
            outputs[1] = middle;
            outputs[2] = middle - width;
        }
    }
}
