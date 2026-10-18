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

    internal sealed class State(int period, bool candleRanges) : IIndicatorState
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
            double total = _minimum.Value + _maximum.Value;
            if (total == 0) return 0; // NOSONAR: S1244 - Exact cancellation and signed zeros use canonical positive zero.
            // Scaling by one half commutes with rounding while the result
            // remains normal. Overflow and the subnormal boundary retain the
            // exact accumulator to avoid intermediate overflow/double rounding.
            if (FrameworkCompatibility.IsFinite(total) && Math.Abs(total) >= 4.450147717014403E-308)
                return total * .5;
            var sum = new ExactMeanAccumulator();
            sum.Add(_minimum.Value);
            sum.Add(_maximum.Value);
            return sum.Mean(2);
        }
    }
}

internal sealed class WindowExtremeDeque(long period, bool maximum, bool newestTie = true)
{
    private (double Value, long Index)[] _candidates = [];
    private int _head, _count;
    private long _next;
    internal double Value => _candidates[_head].Value;
    internal long Index => _candidates[_head].Index;

    internal void Reset()
    {
        _head = _count = 0;
        _next = 0;
    }

    internal void Add(double value)
    {
        // Expire before insertion so a full period never needs a spare slot.
        var expired = _next - period;
        while (_count > 0 && _candidates[_head].Index <= expired)
        {
            _head = Slot(1);
            _count--;
        }
        while (_count > 0)
        {
            double last = _candidates[Slot(_count - 1)].Value;
            bool replace = maximum
                ? newestTie ? value >= last : value > last
                : newestTie ? value <= last : value < last;
            if (!replace) break;
            _count--;
        }
        if (_count == _candidates.Length)
        {
            int capacity = (int)Math.Min(period, Math.Min(int.MaxValue, Math.Max(4L, 2L * _candidates.Length)));
            var grown = new (double Value, long Index)[capacity];
            for (int i = 0; i < _count; i++) grown[i] = _candidates[Slot(i)];
            _candidates = grown;
            _head = 0;
        }
        _candidates[Slot(_count++)] = (value, _next);
        _next++;
    }

    private int Slot(int offset)
    {
        int untilWrap = _candidates.Length - _head;
        return offset >= untilWrap ? offset - untilWrap : _head + offset;
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
