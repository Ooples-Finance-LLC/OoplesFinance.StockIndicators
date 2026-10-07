using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Trady-style stochastic of nullable RSI, on a zero-to-one scale.</summary>
/// <remarks>RSI uses the nullable strength convention. After period input rows,
/// a flat or entirely missing RSI window yields one half. Otherwise missing current
/// RSI stays absent. Each final range ratio rounds once; history grows lazily.</remarks>
public sealed class NullableStochasticRsi : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period nullable stochastic RSI.</summary>
    public NullableStochasticRsi(int period = 14)
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Both RSI and stochastic window period.</summary>
    public int Period { get; }

    /// <summary>Unit-interval value, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the selected output is present.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <summary>Calculates nullable inputs with independent state for every enumeration.</summary>
    /// <remarks>Missing-input RSI semantics are inherited from NullableStrengthOscillator.
    /// A later all-missing RSI window still produces the specified one-half result.</remarks>
    public static IEnumerable<double?> FromValues(IEnumerable<double?> values, int period = 14)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        var rsi = NullableStrengthOscillator.FromValues(values, period);
        return Enumerate();
        IEnumerable<double?> Enumerate()
        {
            var window = new NullableRsiRange(period, .5, 1);
            foreach (var value in rsi)
                yield return window.Next(value);
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        StochasticRsiReference
                            .Nullable(bars.Select(b => (double?)b.Close).ToArray(), Period)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly IMultiOutputState _rsi = (IMultiOutputState)
            new NullableStrengthOscillator(period).CreateState();
        private readonly NullableRsiRange _window = new(period, .5, 1);

        public void Reset()
        {
            _rsi.Reset();
            _window.Reset();
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            Span<double> rsi = stackalloc double[2];
            _rsi.Update(bar, rsi);
            var value = _window.Next(rsi[1] > 0 ? rsi[0] : null);
            outputs[0] = value ?? 0;
            outputs[1] = value.HasValue ? 1 : 0;
        }
    }
}

/// <summary>Full-window stochastic of Wilder RSI with flat value zero, SMA smoothing and an SMA signal.</summary>
/// <remarks>RSI uses the hundred-flat convention. Stochastic waits for a complete
/// window of published RSI values, then each smoothing stage waits for its full
/// input window. Values use a zero-to-one-hundred scale. Every ratio and mean rounds
/// once. Period sums cannot overflow startup arithmetic and histories grow lazily.</remarks>
public sealed class SmoothedStochasticRsi : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive RSI, stochastic, signal and oscillator-smoothing periods.</summary>
    public SmoothedStochasticRsi(
        int rsiPeriod = 14,
        int stochasticPeriod = 14,
        int signalPeriod = 3,
        int smoothPeriod = 1
    )
        : base(4)
    {
        if (rsiPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(rsiPeriod));
        if (stochasticPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(stochasticPeriod));
        if (signalPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        if (smoothPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(smoothPeriod));
        RsiPeriod = rsiPeriod;
        StochasticPeriod = stochasticPeriod;
        SignalPeriod = signalPeriod;
        SmoothPeriod = smoothPeriod;
    }

    /// <summary>Wilder RSI seed and smoothing length.</summary>
    public int RsiPeriod { get; }

    /// <summary>RSI range window length.</summary>
    public int StochasticPeriod { get; }

    /// <summary>SMA signal length.</summary>
    public int SignalPeriod { get; }

    /// <summary>Stochastic SMA smoothing length.</summary>
    public int SmoothPeriod { get; }

    /// <summary>Smoothed stochastic, or zero before startup.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>Signal, or zero before startup.</summary>
    public IIndicatorOutput Signal => Outputs[1];

    /// <summary>Stochastic presence.</summary>
    public IIndicatorOutput IsDefined => Outputs[2];

    /// <summary>Signal presence.</summary>
    public IIndicatorOutput IsSignalDefined => Outputs[3];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(RsiPeriod, StochasticPeriod, SignalPeriod, SmoothPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        StochasticRsiReference.Smoothed(
                            bars,
                            RsiPeriod,
                            StochasticPeriod,
                            SignalPeriod,
                            SmoothPeriod
                        )[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int rsiPeriod, int stochPeriod, int signalPeriod, int smoothPeriod)
        : IMultiOutputState
    {
        private readonly IMultiOutputState _rsi = (IMultiOutputState)
            new WilderStrengthOscillator(
                rsiPeriod,
                WilderStrengthConvention.RsiHundredFlat
            ).CreateState();
        private readonly NullableRsiRange _window = new(stochPeriod, 0, 100);
        private readonly FullMean _smooth = new(smoothPeriod),
            _signal = new(signalPeriod);

        public void Reset()
        {
            _rsi.Reset();
            _window.Reset();
            _smooth.Reset();
            _signal.Reset();
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            Span<double> rsi = stackalloc double[2];
            _rsi.Update(bar, rsi);
            if (rsi[1] <= 0)
                return;
            var raw = _window.Next(rsi[0]);
            if (!raw.HasValue)
                return;
            var value = _smooth.Next(raw.Value);
            if (!value.HasValue)
                return;
            outputs[0] = value.Value;
            outputs[2] = 1;
            var signal = _signal.Next(value.Value);
            if (signal.HasValue)
            {
                outputs[1] = signal.Value;
                outputs[3] = 1;
            }
        }
    }

    private sealed class FullMean(int period)
    {
        private readonly Queue<double> _values = new();
        private ExactMeanAccumulator _sum;

        internal void Reset()
        {
            _values.Clear();
            _sum = default;
        }

        internal double? Next(double value)
        {
            if (_values.Count == period)
                _sum.Add(_values.Dequeue(), -1);
            _values.Enqueue(value);
            _sum.Add(value);
            return _values.Count == period ? _sum.Mean(period) : null;
        }
    }
}

internal sealed class NullableRsiRange(int period, double flat, int scale)
{
    private readonly LinkedList<(long Index, double Value)> _high = new(),
        _low = new();
    private long _next;
    private int _count;

    internal void Reset()
    {
        _high.Clear();
        _low.Clear();
        _next = 0;
        _count = 0;
    }

    internal double? Next(double? value)
    {
        var index = _next++;
        if (_count < period)
            _count++;
        Update(_high, true);
        Update(_low, false);
        if (_count < period)
            return null;
        if (_high.First is null)
            return flat;
        var high = _high.First.Value.Value;
        var low = _low.First!.Value.Value;
        if (high == low) // NOSONAR: Exact flat ranges have a distinct defined formula.
            return flat;
        if (!value.HasValue)
            return null;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(value.Value, scale);
        numerator.Add(low, -scale);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(high);
        denominator.Add(low, -1);
        return numerator.Ratio(denominator);
        void Update(LinkedList<(long Index, double Value)> deque, bool maximum)
        {
            while (deque.First is { } head && head.Value.Index <= index - period)
                deque.RemoveFirst();
            if (!value.HasValue)
                return;
            while (
                deque.Last is { } tail
                && (maximum ? tail.Value.Value <= value.Value : tail.Value.Value >= value.Value)
            )
                deque.RemoveLast();
            deque.AddLast((index, value.Value));
        }
    }
}
