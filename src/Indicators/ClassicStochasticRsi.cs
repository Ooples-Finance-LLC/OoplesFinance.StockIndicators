using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window stochastic of Wilder RSI with any classical signal average.</summary>
/// <remarks>RSI uses the zero-flat convention and exact gain/loss differences. The
/// complete RSI window maps to [0,100], with zero for an exactly flat window. The
/// signal follows ClassicMovingAverage's seeding and suppression; both K and D are
/// withheld until the signal is defined. RSI and signal suppression are independent.
/// First-price seeding applies only to exponential signal stages, not to RSI.
/// Every ratio and averaging stage follows its independently verified rounding
/// contract. Lookbacks use Int64 and histories grow only with observations.</remarks>
public sealed class ClassicStochasticRsi : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive RSI/range/signal periods and nonnegative suppression.</summary>
    public ClassicStochasticRsi(
        int rsiPeriod = 14,
        int stochasticPeriod = 5,
        int signalPeriod = 3,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool firstPriceSignalSeed = false,
        int rsiSuppression = 0,
        int averageSuppression = 0
    )
        : base(4)
    {
        if (stochasticPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(stochasticPeriod));
        Rsi = new(rsiPeriod, WilderStrengthConvention.RsiZeroFlat, rsiSuppression);
        SignalAverage = new(signalPeriod, method, firstPriceSignalSeed, averageSuppression);
        StochasticPeriod = stochasticPeriod;
    }

    /// <summary>Wilder gain/loss seed and recurrence period.</summary>
    public int RsiPeriod => Rsi.Period;

    /// <summary>Number of published RSI observations in the stochastic range.</summary>
    public int StochasticPeriod { get; }

    /// <summary>Signal averaging period.</summary>
    public int SignalPeriod => SignalAverage.Period;

    /// <summary>Signal formula.</summary>
    public ClassicAverageMethod Method => SignalAverage.Method;

    /// <summary>First-price seeding for exponential signal stages.</summary>
    public bool FirstPriceSignalSeed => SignalAverage.FirstPriceSeed;

    /// <summary>Additional RSI observations withheld before range calculation.</summary>
    public int RsiSuppression => Rsi.UnstablePeriods;

    /// <summary>Additional signal startup suppression.</summary>
    public int AverageSuppression => SignalAverage.Suppression;

    /// <summary>Unsmoothed stochastic RSI after signal startup.</summary>
    public IIndicatorOutput K => Outputs[0];

    /// <summary>Classical signal average.</summary>
    public IIndicatorOutput D => Outputs[1];

    /// <summary>K presence.</summary>
    public IIndicatorOutput IsKDefined => Outputs[2];

    /// <summary>D presence.</summary>
    public IIndicatorOutput IsDDefined => Outputs[3];
    internal WilderStrengthOscillator Rsi { get; }
    internal ClassicMovingAverage SignalAverage { get; }
    internal long First =>
        (long)RsiPeriod + RsiSuppression + StochasticPeriod - 1 + SignalAverage.First;

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, First);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        ClassicStochasticRsiReference
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
        private readonly IMultiOutputState _rsi,
            _signal;
        private readonly NullableRsiRange _range;
        private readonly double[] _strength = new double[2],
            _average = new double[2];

        internal State(ClassicStochasticRsi owner)
        {
            _rsi = (IMultiOutputState)owner.Rsi.CreateState();
            _signal = (IMultiOutputState)owner.SignalAverage.CreateState();
            _range = new(owner.StochasticPeriod, 0, 100);
        }

        public void Dispose()
        {
            if (_signal is IDisposable disposable)
                disposable.Dispose();
        }

        public void Reset()
        {
            _rsi.Reset();
            _signal.Reset();
            _range.Reset();
            Array.Clear(_strength, 0, 2);
            Array.Clear(_average, 0, 2);
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            _rsi.Update(bar, _strength);
            if (_strength[1] == 0)
                return;
            var raw = _range.Next(_strength[0]);
            if (!raw.HasValue)
                return;
            var sample = new Bar(bar.Time, raw.Value, raw.Value, raw.Value, raw.Value, 0);
            _signal.Update(sample, _average);
            if (_average[1] == 0)
                return;
            outputs[0] = raw.Value;
            outputs[1] = _average[0];
            outputs[2] = outputs[3] = 1;
        }
    }
}
