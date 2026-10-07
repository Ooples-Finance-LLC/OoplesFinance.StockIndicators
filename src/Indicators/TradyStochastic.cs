using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window high/low stochastic with the Trady startup and flat value of fifty.</summary>
/// <remarks>Evaluates 100*(close-low)/(high-low) with exact differences and one final
/// rounding. Before the complete window, or when high equals low, returns fifty.</remarks>
public sealed class FiftySeedStochastic : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period raw stochastic.</summary>
    public FiftySeedStochastic(int period = 14)
    {
        StochasticSmaState.Validate(period, 1, 1);
        Period = period;
    }

    /// <summary>High/low window length.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new RawState(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => StochasticSmaReference.Raw(bars, Period),
                IndicatorErrorBudget.Exact
            ),
        ];

    internal sealed class RawState(int period, double flat = 50) : IIndicatorState
    {
        private readonly WindowExtremeDeque _high = new(period, true),
            _low = new(period, false);
        private int _count;
        internal bool IsReady => _count >= period;

        public void Reset()
        {
            _high.Reset();
            _low.Reset();
            _count = 0;
        }

        public double Update(in Bar bar) => Next(bar).Publish();

        internal RocBankValue Next(in Bar bar)
        {
            _high.Add(bar.High);
            _low.Add(bar.Low);
            if (_count < period)
                _count++;
            if (_count < period || _high.Value == _low.Value) // NOSONAR: Exactly flat ranges use fifty; a tiny nonzero range still has a ratio.
                return new RocBankValue(flat);
            var top = new ExactMeanAccumulator();
            top.Add(bar.Close, 100);
            top.Add(_low.Value, -100);
            var bottom = new ExactMeanAccumulator();
            bottom.Add(_high.Value);
            bottom.Add(_low.Value, -1);
            for (var shift = 0; ; shift += 1024)
            {
                var scaled = bottom;
                scaled.ScaleByPowerOfTwo(shift);
                var value = top.Ratio(scaled);
                if (double.IsFinite(value))
                    return new RocBankValue(value, shift);
            }
        }
    }
}

/// <summary>Trady-style SMA stochastic K, D and J with independently explicit presence.</summary>
/// <remarks>K is the kPeriod SMA of FiftySeedStochastic; D is its dPeriod SMA;
/// J=3*K-2*D. Each average and final formula rounds once. The smoothing windows
/// skip missing entries once their calendar windows are complete. Unpublished
/// ratios and averages retain binary64 precision with an extended upper exponent. kPeriod=1
/// selects Fast, kPeriod=3 selects Slow, and configurable kPeriod selects Full.
/// Chaining replaces close while retaining candle highs/lows. Storage grows lazily.</remarks>
public sealed class StochasticSmaKdj : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates K/D/J with positive price and smoothing periods.</summary>
    public StochasticSmaKdj(int period = 14, int kPeriod = 3, int dPeriod = 3)
        : base(6)
    {
        StochasticSmaState.Validate(period, kPeriod, dPeriod);
        Period = period;
        KPeriod = kPeriod;
        DPeriod = dPeriod;
    }

    /// <summary>Price window length.</summary>
    public int Period { get; }

    /// <summary>K smoothing length; one retains the raw stochastic.</summary>
    public int KPeriod { get; }

    /// <summary>D smoothing length.</summary>
    public int DPeriod { get; }

    /// <summary>K, or zero when absent.</summary>
    public IIndicatorOutput K => Outputs[0];

    /// <summary>D, or zero when absent.</summary>
    public IIndicatorOutput D => Outputs[1];

    /// <summary>3*K-2*D, or zero when absent.</summary>
    public IIndicatorOutput J => Outputs[2];

    /// <summary>K presence.</summary>
    public IIndicatorOutput IsKDefined => Outputs[3];

    /// <summary>D presence.</summary>
    public IIndicatorOutput IsDDefined => Outputs[4];

    /// <summary>J presence.</summary>
    public IIndicatorOutput IsJDefined => Outputs[5];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, KPeriod, DPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        StochasticSmaReference.Output(bars, Period, KPeriod, DPeriod, slot, false),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, int k, int d) : IMultiOutputState
    {
        private readonly StochasticSmaState _state = new(period, k, d);

        public void Reset() => _state.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var (a, b) = _state.Next(bar);
            if (a.HasValue)
            {
                output[0] = a.Value.Publish();
                output[3] = 1;
            }
            if (b.HasValue)
            {
                output[1] = b.Value.Publish();
                output[4] = 1;
            }
            if (
                !a.HasValue
                || !b.HasValue
                || !double.IsFinite(output[0])
                || !double.IsFinite(output[1])
            )
                return;
            var j = new ExactMeanAccumulator();
            a.Value.AddTo(ref j, 3);
            b.Value.AddTo(ref j, -2);
            output[2] = j.Mean(1);
            output[5] = 1;
        }
    }
}

/// <summary>K minus D for the same nullable SMA stochastic conventions as StochasticSmaKdj.</summary>
/// <remarks>Only the selected oscillator is published; the unselected J formula is not evaluated.</remarks>
public sealed class StochasticSmaDifference : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates an oscillator with positive price and smoothing periods.</summary>
    public StochasticSmaDifference(int period = 14, int kPeriod = 3, int dPeriod = 3)
        : base(2)
    {
        StochasticSmaState.Validate(period, kPeriod, dPeriod);
        Period = period;
        KPeriod = kPeriod;
        DPeriod = dPeriod;
    }

    /// <summary>Price window length.</summary>
    public int Period { get; }

    /// <summary>K smoothing length.</summary>
    public int KPeriod { get; }

    /// <summary>D smoothing length.</summary>
    public int DPeriod { get; }

    /// <summary>K minus D, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>Oscillator presence.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, KPeriod, DPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        StochasticSmaReference.Output(bars, Period, KPeriod, DPeriod, slot, true),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, int k, int d) : IMultiOutputState
    {
        private readonly StochasticSmaState _state = new(period, k, d);

        public void Reset() => _state.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var (a, b) = _state.Next(bar);
            if (!a.HasValue || !b.HasValue)
                return;
            var difference = new ExactMeanAccumulator();
            a.Value.AddTo(ref difference);
            b.Value.AddTo(ref difference, -1);
            output[0] = difference.Mean(1);
            output[1] = 1;
        }
    }
}

internal sealed class StochasticSmaState(int period, int k, int d)
{
    private readonly FiftySeedStochastic.RawState _raw = new(period);
    private readonly NullableMean _k = new(k),
        _d = new(d);

    internal static void Validate(int period, int k, int d)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (k < 1)
            throw new ArgumentOutOfRangeException(nameof(k));
        if (d < 1)
            throw new ArgumentOutOfRangeException(nameof(d));
    }

    internal void Reset()
    {
        _raw.Reset();
        _k.Reset();
        _d.Reset();
    }

    internal (RocBankValue? K, RocBankValue? D) Next(in Bar bar)
    {
        var raw = _raw.Next(bar);
        var a = _k.Add(raw);
        return (a, _d.Add(a));
    }

    private sealed class NullableMean(int period)
    {
        private readonly Queue<RocBankValue?> _values = new();
        private ExactMeanAccumulator _sum;
        private int _known;

        internal void Reset()
        {
            _values.Clear();
            _sum = default;
            _known = 0;
        }

        internal RocBankValue? Add(RocBankValue? value)
        {
            if (_values.Count == period && _values.Dequeue() is { } old)
            {
                old.AddTo(ref _sum, -1);
                _known--;
            }
            _values.Enqueue(value);
            if (value.HasValue)
            {
                value.Value.AddTo(ref _sum);
                _known++;
            }
            return _values.Count == period && _known > 0
                ? RocBankValue.Round(_sum, count: _known)
                : null;
        }
    }
}
