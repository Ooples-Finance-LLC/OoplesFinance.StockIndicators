using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Returns one for opposite bearish/non-bearish colors and strict body containment, otherwise zero.</summary>
/// <remarks>Optional shadow containment is strict. A non-bearish doji can match plain Harami, but cannot match a directional pattern.</remarks>
public sealed class StrictHaramiCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a containment pattern. Directional periods count consecutive open/close transitions.</summary>
    public StrictHaramiCandle(bool containedShadows = false)
    {
        ContainedShadows = containedShadows;
    }

    /// <summary>Whether both shadows must also lie strictly inside the prior range.</summary>
    public bool ContainedShadows { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StrictHaramiState(0, 1, ContainedShadows);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars => StrictHaramiReference.Evaluate(bars, 0, 1, ContainedShadows),
                0,
                0
            ),
        ];
}

/// <summary>Returns one for a strictly bullish contained body after strictly falling opens and closes, otherwise zero.</summary>
/// <remarks>Optional shadow containment is strict. A non-bearish doji can match plain Harami, but cannot match a directional pattern.</remarks>
public sealed class BullishHaramiPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a containment pattern. Directional periods count consecutive open/close transitions.</summary>
    public BullishHaramiPattern(int period = 3, bool containedShadows = false)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        ContainedShadows = containedShadows;
    }

    /// <summary>Whether both shadows must also lie strictly inside the prior range.</summary>
    public bool ContainedShadows { get; }

    /// <summary>Number of strictly ordered open/close transitions ending on the prior candle.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 0;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StrictHaramiState(1, Period, ContainedShadows);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars => StrictHaramiReference.Evaluate(bars, 1, Period, ContainedShadows),
                0,
                0
            ),
        ];
}

/// <summary>Returns one for a strictly bearish contained body after strictly rising opens and closes, otherwise zero.</summary>
/// <remarks>Optional shadow containment is strict. A non-bearish doji can match plain Harami, but cannot match a directional pattern.</remarks>
public sealed class BearishHaramiPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a containment pattern. Directional periods count consecutive open/close transitions.</summary>
    public BearishHaramiPattern(int period = 3, bool containedShadows = false)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        ContainedShadows = containedShadows;
    }

    /// <summary>Whether both shadows must also lie strictly inside the prior range.</summary>
    public bool ContainedShadows { get; }

    /// <summary>Number of strictly ordered open/close transitions ending on the prior candle.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => 0;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new StrictHaramiState(-1, Period, ContainedShadows);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, 0, 1),
            IndicatorValidationRule.Reference(
                0,
                bars => StrictHaramiReference.Evaluate(bars, -1, Period, ContainedShadows),
                0,
                0
            ),
        ];
}

internal sealed class StrictHaramiState(int direction, int period, bool shadows) : IIndicatorState
{
    private Bar _previous;
    private bool _initialized;
    private int _trend;

    public void Reset()
    {
        _previous = default;
        _initialized = false;
        _trend = 0;
    }

    public double Update(in Bar bar)
    {
        var match = false;
        if (_initialized)
        {
            var priorBearish = _previous.Close < _previous.Open;
            var bearish = bar.Close < bar.Open;
            match =
                priorBearish != bearish
                && (
                    priorBearish
                        ? _previous.Open > bar.Close && _previous.Close < bar.Open
                        : _previous.Open < bar.Close && _previous.Close > bar.Open
                );
            if (shadows)
                match &= _previous.High > bar.High && _previous.Low < bar.Low;
            if (direction != 0)
                match &= _trend >= period && (direction > 0 ? bar.Close > bar.Open : bearish);
            var transition =
                direction > 0
                    ? bar.Open < _previous.Open && bar.Close < _previous.Close
                    : bar.Open > _previous.Open && bar.Close > _previous.Close;
            _trend = transition
                ? _trend == period
                    ? period
                    : _trend + 1
                : 0;
        }
        _previous = bar;
        _initialized = true;
        return match ? 1 : 0;
    }
}

internal static class StrictHaramiReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int direction,
        int period,
        bool shadows
    )
    {
        var result = new double[bars.Count];
        for (var i = 1; i < bars.Count; i++)
        {
            var p = bars[i - 1];
            var c = bars[i];
            if ((p.Close < p.Open) == (c.Close < c.Open))
                continue;
            if (
                Math.Min(c.Open, c.Close) <= Math.Min(p.Open, p.Close)
                || Math.Max(c.Open, c.Close) >= Math.Max(p.Open, p.Close)
            )
                continue;
            if (shadows && (c.High >= p.High || c.Low <= p.Low))
                continue;
            if (direction != 0)
            {
                if (i - 1 < period || (direction > 0 ? c.Close <= c.Open : c.Close >= c.Open))
                    continue;
                var matched = true;
                for (var j = i - period; j < i; j++)
                    matched &=
                        direction > 0
                            ? bars[j].Open < bars[j - 1].Open && bars[j].Close < bars[j - 1].Close
                            : bars[j].Open > bars[j - 1].Open && bars[j].Close > bars[j - 1].Close;
                if (!matched)
                    continue;
            }
            result[i] = 1;
        }
        return result;
    }
}
