using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Complete-window stochastic with independently selected classical K and D averages.</summary>
/// <remarks>The raw ratio is 100*(close-low)/(high-low), or zero for an exactly
/// flat range. K and D publish together after both averaging stages start.
/// Use kPeriod=1 for fast stochastic. Ratios and averages round independently
/// with extended upper exponents, preserving finite results after hidden overflow.
/// Only published overflow is rejected. Chaining replaces close and retains
/// candle ranges. Positive period-one identities and lazy histories are supported.</remarks>
public sealed class ClassicStochastic : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive range/K/D periods and nonnegative averaging suppression.</summary>
    public ClassicStochastic(
        int period = 5,
        int kPeriod = 3,
        int dPeriod = 3,
        ClassicAverageMethod kMethod = ClassicAverageMethod.Sma,
        ClassicAverageMethod dMethod = ClassicAverageMethod.Sma,
        bool firstPriceSeed = false,
        int suppression = 0
    )
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        KAverage = new(kPeriod, kMethod, firstPriceSeed, suppression);
        DAverage = new(dPeriod, dMethod, firstPriceSeed, suppression);
    }

    /// <summary>High/low window length.</summary>
    public int Period { get; }

    /// <summary>K averaging period; one selects raw K.</summary>
    public int KPeriod => KAverage.Period;

    /// <summary>D averaging period.</summary>
    public int DPeriod => DAverage.Period;

    /// <summary>K formula.</summary>
    public ClassicAverageMethod KMethod => KAverage.Method;

    /// <summary>D formula.</summary>
    public ClassicAverageMethod DMethod => DAverage.Method;

    /// <summary>First-price seeding for exponential stages.</summary>
    public bool FirstPriceSeed => KAverage.FirstPriceSeed;

    /// <summary>Additional averaging startup suppression.</summary>
    public int Suppression => KAverage.Suppression;

    /// <summary>K after D startup.</summary>
    public IIndicatorOutput K => Outputs[0];

    /// <summary>D signal.</summary>
    public IIndicatorOutput D => Outputs[1];

    /// <summary>K presence.</summary>
    public IIndicatorOutput IsKDefined => Outputs[2];

    /// <summary>D presence.</summary>
    public IIndicatorOutput IsDDefined => Outputs[3];
    internal ClassicMovingAverage KAverage { get; }
    internal ClassicMovingAverage DAverage { get; }
    internal long First => Period - 1L + KAverage.First + DAverage.First;

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, First);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        ClassicStochasticReference
                            .Values(bars, this)[slot % 2]
                            .Select(v =>
                                slot < 2 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State : IMultiOutputState, IDisposable
    {
        private readonly FiftySeedStochastic.RawState _raw;
        private readonly IWideAverageState _k,
            _d;
        private readonly double[] _ks = new double[2],
            _ds = new double[2];

        internal State(ClassicStochastic owner)
        {
            _raw = new(owner.Period, 0);
            _k = (IWideAverageState)owner.KAverage.CreateState();
            _d = (IWideAverageState)owner.DAverage.CreateState();
        }

        public void Reset()
        {
            _raw.Reset();
            _k.Reset();
            _d.Reset();
            Array.Clear(_ks, 0, 2);
            Array.Clear(_ds, 0, 2);
        }

        public void Dispose()
        {
            if (_k is IDisposable k)
                k.Dispose();
            if (_d is IDisposable d)
                d.Dispose();
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            var raw = _raw.Next(bar);
            if (!_raw.IsReady)
                return;
            _k.UpdateUnits(ExactVarianceWindow.Units(raw.Mantissa) << raw.UpperShift, _ks);
            if (_ks[1] == 0)
                return;
            _d.UpdateUnits(_k.RoundedUnits, _ds);
            if (_ds[1] == 0)
                return;
            outputs[0] = _ks[0];
            outputs[1] = _ds[0];
            outputs[2] = outputs[3] = 1;
        }
    }
}
