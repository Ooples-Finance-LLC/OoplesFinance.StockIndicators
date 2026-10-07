using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Absolute zero-based index of the most recent minimum or maximum close in a rolling window.</summary>
/// <remarks>Equal prices select the newest index. Startup uses available history; storage grows lazily up to Period.</remarks>
public sealed class WindowExtremeIndex : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a minimum-close index, or maximum-close index when maximum is true.</summary>
    public WindowExtremeIndex(int period = 30, bool maximum = false)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        Maximum = maximum;
    }

    /// <summary>Positive rolling window length.</summary>
    public int Period { get; }

    /// <summary>Whether the highest close is selected.</summary>
    public bool Maximum { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Maximum);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars => WindowExtremeReference.Indices(bars, Period, Maximum),
                0,
                0
            ),
        ];

    private sealed class State(int period, bool maximum) : IIndicatorState
    {
        private readonly WindowExtremeDeque _window = new(period, maximum);

        public void Reset() => _window.Reset();

        public double Update(in Bar bar)
        {
            _window.Add(bar.Close);
            return _window.Index;
        }
    }
}

/// <summary>Midpoint between a rolling maximum and minimum, rounded once without overflowing their sum.</summary>
/// <remarks>By default both extrema use closes. When candleRanges is true they use high and low.
/// Startup uses available history; storage grows lazily up to Period.</remarks>
public sealed class WindowRangeMidpoint : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a rolling close midpoint or high-low range midpoint.</summary>
    public WindowRangeMidpoint(int period = 14, bool candleRanges = false)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        CandleRanges = candleRanges;
    }

    /// <summary>Positive rolling window length.</summary>
    public int Period { get; }

    /// <summary>Whether to use candle high/low extrema instead of close extrema.</summary>
    public bool CandleRanges { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, CandleRanges);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars => WindowExtremeReference.Midpoints(bars, Period, CandleRanges),
                0,
                0
            ),
        ];

    private sealed class State(int period, bool candleRanges) : IIndicatorState
    {
        private readonly WindowExtremeDeque _minimum = new(period, false);
        private readonly WindowExtremeDeque _maximum = new(period, true);

        public void Reset()
        {
            _minimum.Reset();
            _maximum.Reset();
        }

        public double Update(in Bar bar)
        {
            _minimum.Add(candleRanges ? bar.Low : bar.Close);
            _maximum.Add(candleRanges ? bar.High : bar.Close);
            var sum = new ExactMeanAccumulator();
            sum.Add(_minimum.Value);
            sum.Add(_maximum.Value);
            return sum.Mean(2);
        }
    }
}

internal sealed class WindowExtremeDeque(long period, bool maximum, bool newestTie = true)
{
    private readonly LinkedList<(double Value, long Index)> _candidates = new();
    private long _next;
    internal double Value => _candidates.First!.Value.Value;
    internal long Index => _candidates.First!.Value.Index;

    internal void Reset()
    {
        _candidates.Clear();
        _next = 0;
    }

    internal void Add(double value)
    {
        while (
            _candidates.Last is { } last
            && (
                maximum
                    ? newestTie
                        ? value >= last.Value.Value
                        : value > last.Value.Value
                    : newestTie
                        ? value <= last.Value.Value
                        : value < last.Value.Value
            )
        )
            _candidates.RemoveLast();
        _candidates.AddLast((value, _next));
        var expired = _next - period;
        while (_candidates.First!.Value.Index <= expired)
            _candidates.RemoveFirst();
        _next++;
    }
}

internal static class WindowExtremeReference
{
    internal static IReadOnlyList<double> Indices(IReadOnlyList<Bar> bars, int period, bool maximum)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var best = i;
            for (var j = i - 1; j >= Math.Max(0, i - period + 1); j--)
                if (maximum ? bars[j].Close > bars[best].Close : bars[j].Close < bars[best].Close)
                    best = j;
            result[i] = best;
        }
        return result;
    }

    internal static IReadOnlyList<double> Midpoints(
        IReadOnlyList<Bar> bars,
        int period,
        bool candleRanges
    )
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - period + 1)).Take(Math.Min(i + 1, period));
            var low = window.Min(b => candleRanges ? b.Low : b.Close);
            var high = window.Max(b => candleRanges ? b.High : b.Close);
            result[i] = (
                (ReferenceFraction.FromDouble(low) + ReferenceFraction.FromDouble(high))
                / new ReferenceFraction(2)
            ).ToDouble();
        }
        return result;
    }
}
