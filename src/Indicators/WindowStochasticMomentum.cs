using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Close minus the midpoint of the complete high/low window.</summary>
/// <remarks>The complete expression rounds once. Startup is absent, with an
/// explicit presence output. High/low history grows lazily; chained sources
/// replace close only. Genuine final overflow is rejected by the runtime.</remarks>
public sealed class WindowStochasticMomentum
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period window momentum.</summary>
    public WindowStochasticMomentum(int period = 14)
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Price window length.</summary>
    public int Period { get; }

    /// <summary>Momentum, or zero before startup.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the complete window exists.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => StochasticMomentumReference.Raw(bars, Period, slot),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly StochasticMomentumWindow _window = new(period);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            var value = _window.Next(bar);
            if (!value.HasValue)
                return;
            outputs[0] = value.Value.Delta.Publish();
            outputs[1] = 1;
        }
    }
}

/// <summary>Double EMA of midpoint displacement divided by double EMA of window range, plus an EMA signal.</summary>
/// <remarks>Both smoothing stages seed from the first complete price window. Each
/// displacement, range and convex recurrence rounds once at binary64 precision,
/// with an extended upper exponent for unpublished values. The index is 200 times
/// smoothed displacement divided by smoothed range. Zero range makes index and
/// signal absent; the next defined index seeds a new signal. Each final index and
/// signal rounds once. Positive periods use lazy history and constant smoothing state.</remarks>
public sealed class DoubleSmoothedStochasticMomentum
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates the index and signal with positive price and smoothing periods.</summary>
    public DoubleSmoothedStochasticMomentum(
        int period = 13,
        int firstPeriod = 25,
        int secondPeriod = 2,
        int signalPeriod = 3
    )
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (firstPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(firstPeriod));
        if (secondPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(secondPeriod));
        if (signalPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(signalPeriod));
        Period = period;
        FirstPeriod = firstPeriod;
        SecondPeriod = secondPeriod;
        SignalPeriod = signalPeriod;
    }

    /// <summary>High/low window length.</summary>
    public int Period { get; }

    /// <summary>First EMA length.</summary>
    public int FirstPeriod { get; }

    /// <summary>Second EMA length.</summary>
    public int SecondPeriod { get; }

    /// <summary>Index signal EMA length.</summary>
    public int SignalPeriod { get; }

    /// <summary>Index, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>Signal, or zero when absent.</summary>
    public IIndicatorOutput Signal => Outputs[1];

    /// <summary>Index presence.</summary>
    public IIndicatorOutput IsDefined => Outputs[2];

    /// <summary>Signal presence.</summary>
    public IIndicatorOutput IsSignalDefined => Outputs[3];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, FirstPeriod, SecondPeriod, SignalPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        StochasticMomentumReference.Index(
                            bars,
                            Period,
                            FirstPeriod,
                            SecondPeriod,
                            SignalPeriod,
                            slot
                        ),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, int first, int second, int signalPeriod)
        : IMultiOutputState
    {
        private readonly StochasticMomentumWindow _window = new(period);
        private RocBankValue _delta1,
            _delta2,
            _range1,
            _range2;
        private bool _started,
            _signalStarted;
        private double _signal;

        public void Reset()
        {
            _window.Reset();
            _delta1 = _delta2 = _range1 = _range2 = default;
            _started = _signalStarted = false;
            _signal = 0;
        }

        private static RocBankValue Smooth(RocBankValue value, RocBankValue previous, int period)
        {
            var sum = new ExactMeanAccumulator();
            value.AddTo(ref sum, 2);
            previous.AddTo(ref sum, period - 1L);
            return RocBankValue.Round(sum, count: (long)period + 1);
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            var value = _window.Next(bar);
            if (!value.HasValue)
                return;
            if (!_started)
            {
                _delta1 = _delta2 = value.Value.Delta;
                _range1 = _range2 = value.Value.Range;
                _started = true;
            }
            else
            {
                _delta1 = Smooth(value.Value.Delta, _delta1, first);
                _delta2 = Smooth(_delta1, _delta2, second);
                _range1 = Smooth(value.Value.Range, _range1, first);
                _range2 = Smooth(_range1, _range2, second);
            }
            var denominator = new ExactMeanAccumulator();
            _range2.AddTo(ref denominator);
            if (denominator.IsExactlyZero)
            {
                _signalStarted = false;
                return;
            }
            var numerator = new ExactMeanAccumulator();
            _delta2.AddTo(ref numerator, 200);
            var index = numerator.Ratio(denominator);
            outputs[0] = index;
            outputs[2] = 1;
            if (!FrameworkCompatibility.IsFinite(index))
                return;
            _signal = _signalStarted
                ? Smooth(new RocBankValue(index), new RocBankValue(_signal), signalPeriod).Publish()
                : index;
            _signalStarted = true;
            outputs[1] = _signal;
            outputs[3] = 1;
        }
    }
}

internal sealed class StochasticMomentumWindow(int period)
{
    private readonly WindowExtremeDeque _high = new(period, true),
        _low = new(period, false);
    private int _count;

    internal void Reset()
    {
        _high.Reset();
        _low.Reset();
        _count = 0;
    }

    internal (RocBankValue Delta, RocBankValue Range)? Next(in Bar bar)
    {
        _high.Add(bar.High);
        _low.Add(bar.Low);
        if (_count < period)
            _count++;
        if (_count < period)
            return null;
        var delta = new ExactMeanAccumulator();
        delta.Add(bar.Close, 2);
        delta.Add(_high.Value, -1);
        delta.Add(_low.Value, -1);
        var range = new ExactMeanAccumulator();
        range.Add(_high.Value);
        range.Add(_low.Value, -1);
        return (RocBankValue.Round(delta, count: 2), RocBankValue.Round(range));
    }
}
