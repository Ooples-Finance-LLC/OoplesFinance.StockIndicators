using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window stochastic K, D and configurable J with simple or Wilder smoothing.</summary>
/// <remarks>The raw value is 100*(close-low)/(high-low), or zero for an exactly flat
/// complete window. Simple averages wait for each complete window; Wilder averages
/// seed at the first available value. K publishes independently, while D and J wait
/// for the signal. J=kFactor*K-dFactor*D rounds once. Each raw ratio and averaging
/// stage rounds with extended upper exponents, preserving unpublished cancellation.
/// Chaining replaces close and retains candle ranges. All histories grow lazily.</remarks>
public sealed class WindowStochasticKdj : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive price/K/D periods and finite positive J factors.</summary>
    public WindowStochasticKdj(
        int period = 14,
        int kPeriod = 3,
        int dPeriod = 3,
        bool wilderSmoothing = false,
        double kFactor = 3,
        double dFactor = 2
    )
        : base(6)
    {
        StochasticSmaState.Validate(period, kPeriod, dPeriod);
        if (!double.IsFinite(kFactor) || kFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(kFactor));
        if (!double.IsFinite(dFactor) || dFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(dFactor));
        Period = period;
        KPeriod = kPeriod;
        DPeriod = dPeriod;
        WilderSmoothing = wilderSmoothing;
        KFactor = kFactor;
        DFactor = dFactor;
    }

    /// <summary>High/low window length.</summary>
    public int Period { get; }

    /// <summary>K smoothing period.</summary>
    public int KPeriod { get; }

    /// <summary>D smoothing period.</summary>
    public int DPeriod { get; }

    /// <summary>Whether both smoothers use first-value-seeded Wilder recurrences.</summary>
    public bool WilderSmoothing { get; }

    /// <summary>Positive K coefficient of J.</summary>
    public double KFactor { get; }

    /// <summary>Positive D coefficient subtracted in J.</summary>
    public double DFactor { get; }

    /// <summary>Smoothed K, or zero when absent.</summary>
    public IIndicatorOutput K => Outputs[0];

    /// <summary>Signal D, or zero when absent.</summary>
    public IIndicatorOutput D => Outputs[1];

    /// <summary>Combined J, or zero when absent.</summary>
    public IIndicatorOutput J => Outputs[2];

    /// <summary>K presence.</summary>
    public IIndicatorOutput IsKDefined => Outputs[3];

    /// <summary>D presence.</summary>
    public IIndicatorOutput IsDDefined => Outputs[4];

    /// <summary>J presence.</summary>
    public IIndicatorOutput IsJDefined => Outputs[5];

    /// <inheritdoc/>
    public override int WarmupBars =>
        (int)Math.Min(int.MaxValue, Period - 1L + (WilderSmoothing ? 0 : KPeriod - 1L));

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
                        WindowStochasticReference
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

    private sealed class State(WindowStochasticKdj owner) : IMultiOutputState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private readonly FiftySeedStochastic.RawState _raw = new(owner.Period, 0);
        private readonly Average _k = new(owner.KPeriod, owner.WilderSmoothing),
            _d = new(owner.DPeriod, owner.WilderSmoothing);
        private readonly BigInteger _kFactor = ExactVarianceWindow.Units(owner.KFactor),
            _dFactor = ExactVarianceWindow.Units(owner.DFactor);

        public void Reset()
        {
            _raw.Reset();
            _k.Reset();
            _d.Reset();
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            var raw = _raw.Next(bar);
            if (!_raw.IsReady)
                return;
            var k = _k.Next(raw);
            if (!k.HasValue)
                return;
            outputs[0] = k.Value.Publish();
            outputs[3] = 1;
            var d = _d.Next(k.Value);
            if (!d.HasValue)
                return;
            outputs[1] = d.Value.Publish();
            outputs[4] = 1;
            var ku = ExactVarianceWindow.Units(k.Value.Mantissa) << k.Value.UpperShift;
            var du = ExactVarianceWindow.Units(d.Value.Mantissa) << d.Value.UpperShift;
            outputs[2] = ExactMeanAccumulator.UnitRatio(ku * _kFactor - du * _dFactor, Grid);
            outputs[5] = 1;
        }
    }

    private sealed class Average(int period, bool wilder)
    {
        private readonly Queue<RocBankValue> _window = new();
        private ExactMeanAccumulator _sum;
        private RocBankValue? _last;

        internal void Reset()
        {
            _window.Clear();
            _sum = default;
            _last = null;
        }

        internal RocBankValue? Next(RocBankValue value)
        {
            if (wilder)
            {
                if (!_last.HasValue)
                    return _last = value;
                var total = new ExactMeanAccumulator();
                _last.Value.AddTo(ref total, period - 1L);
                value.AddTo(ref total);
                return _last = RocBankValue.Round(total, count: period);
            }
            if (_window.Count == period)
                _window.Dequeue().AddTo(ref _sum, -1);
            _window.Enqueue(value);
            value.AddTo(ref _sum);
            return _window.Count == period ? RocBankValue.Round(_sum, count: period) : null;
        }
    }
}
