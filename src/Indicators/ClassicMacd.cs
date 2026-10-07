using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>MACD with independent classical fast, slow and signal formulas.</summary>
/// <remarks>Periods and their associated methods are sorted together. Fast and slow
/// averages align their first output to the larger lookback; their rounded difference
/// feeds the signal. All three outputs publish together after signal startup. Each
/// averaging/difference stage rounds once with extended upper exponents, preserving
/// finite results after unpublished overflow. Only published overflow is rejected.
/// Period-one identity is supported for every stage; histories grow lazily.</remarks>
public sealed class ClassicMacd : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive fast/slow/signal periods with independent formulas.</summary>
    public ClassicMacd(
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9,
        ClassicAverageMethod fastMethod = ClassicAverageMethod.Sma,
        ClassicAverageMethod slowMethod = ClassicAverageMethod.Sma,
        ClassicAverageMethod signalMethod = ClassicAverageMethod.Sma,
        bool firstPriceSeed = false,
        int suppression = 0
    )
        : base(6)
    {
        if (slowPeriod < fastPeriod)
        {
            (fastPeriod, slowPeriod) = (slowPeriod, fastPeriod);
            (fastMethod, slowMethod) = (slowMethod, fastMethod);
        }
        FastAverage = new(fastPeriod, fastMethod, firstPriceSeed, suppression);
        SlowAverage = new(slowPeriod, slowMethod, firstPriceSeed, suppression);
        SignalAverage = new(signalPeriod, signalMethod, firstPriceSeed, suppression);
    }

    /// <summary>Normalized fast period.</summary>
    public int FastPeriod => FastAverage.Period;

    /// <summary>Normalized slow period.</summary>
    public int SlowPeriod => SlowAverage.Period;

    /// <summary>Signal period.</summary>
    public int SignalPeriod => SignalAverage.Period;

    /// <summary>Normalized fast formula.</summary>
    public ClassicAverageMethod FastMethod => FastAverage.Method;

    /// <summary>Normalized slow formula.</summary>
    public ClassicAverageMethod SlowMethod => SlowAverage.Method;

    /// <summary>Signal formula.</summary>
    public ClassicAverageMethod SignalMethod => SignalAverage.Method;

    /// <summary>First-price seeding for exponential stages.</summary>
    public bool FirstPriceSeed => FastAverage.FirstPriceSeed;

    /// <summary>Additional classical averaging suppression.</summary>
    public int Suppression => FastAverage.Suppression;

    /// <summary>Fast minus slow after signal startup.</summary>
    public IIndicatorOutput Oscillator => Outputs[0];

    /// <summary>Signal average.</summary>
    public IIndicatorOutput Signal => Outputs[1];

    /// <summary>Oscillator minus signal.</summary>
    public IIndicatorOutput Histogram => Outputs[2];

    /// <summary>Oscillator presence.</summary>
    public IIndicatorOutput IsOscillatorDefined => Outputs[3];

    /// <summary>Signal presence.</summary>
    public IIndicatorOutput IsSignalDefined => Outputs[4];

    /// <summary>Histogram presence.</summary>
    public IIndicatorOutput IsHistogramDefined => Outputs[5];
    internal ClassicMovingAverage FastAverage { get; }
    internal ClassicMovingAverage SlowAverage { get; }
    internal ClassicMovingAverage SignalAverage { get; }
    internal long OscillatorFirst => Math.Max(FastAverage.First, SlowAverage.First);

    /// <inheritdoc/>
    public override int WarmupBars =>
        (int)Math.Min(int.MaxValue, OscillatorFirst + SignalAverage.First);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        ClassicMacdReference
                            .Values(bars, this)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State : IMultiOutputState, IDisposable
    {
        private readonly IWideAverageState _fast,
            _slow,
            _signal;
        private readonly double[] _a = new double[2],
            _b = new double[2],
            _s = new double[2];

        internal State(ClassicMacd owner)
        {
            _fast = (IWideAverageState)owner.FastAverage.CreateAlignedState(owner.OscillatorFirst);
            _slow = (IWideAverageState)owner.SlowAverage.CreateAlignedState(owner.OscillatorFirst);
            _signal = (IWideAverageState)owner.SignalAverage.CreateState();
        }

        public void Reset()
        {
            _fast.Reset();
            _slow.Reset();
            _signal.Reset();
            Array.Clear(_a, 0, 2);
            Array.Clear(_b, 0, 2);
            Array.Clear(_s, 0, 2);
        }

        public void Dispose()
        {
            foreach (var state in new[] { _fast, _slow, _signal })
                if (state is IDisposable disposable)
                    disposable.Dispose();
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            var price = ExactVarianceWindow.Units(bar.Close);
            _fast.UpdateUnits(price, _a);
            _slow.UpdateUnits(price, _b);
            if (_a[1] == 0 || _b[1] == 0)
                return;
            var oscillator = RocBankValue.RoundUnits(_fast.RoundedUnits - _slow.RoundedUnits, 1);
            _signal.UpdateUnits(oscillator, _s);
            if (_s[1] == 0)
                return;
            outputs[0] = ExactMeanAccumulator.UnitRatio(oscillator, 1);
            outputs[1] = _s[0];
            outputs[2] = ExactMeanAccumulator.UnitRatio(oscillator - _signal.RoundedUnits, 1);
            outputs[3] = outputs[4] = outputs[5] = 1;
        }
    }
}
