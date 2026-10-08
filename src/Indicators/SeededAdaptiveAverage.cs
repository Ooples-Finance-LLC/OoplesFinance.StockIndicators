using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Efficiency-adaptive average with last-price seeding and explicit flat-window behavior.</summary>
/// <remarks>The seed is the close at period-1. Optional startup publication returns
/// earlier closes. Efficiency is absolute period change divided by the sum of
/// absolute one-bar changes, first available at period. Fast and slow rates are
/// 2/(length+1); the interpolated rate is squared. Rates, efficiency, complete
/// interpolation, square and complete recurrence each round once. Flat windows
/// either reset to close or use zero/one efficiency for slow/fast smoothing. Exact
/// differences and sums prevent overflow; history grows lazily with input.</remarks>
public sealed class SeededAdaptiveAverage
    : MultiOutputIndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates positive-period adaptive smoothing with explicit startup and optional fast-period clamping.</summary>
    public SeededAdaptiveAverage(
        int period = 10,
        int fastPeriod = 2,
        int slowPeriod = 30,
        bool publishStartup = false,
        bool clampFastPeriod = false,
        bool resetFlat = true,
        bool fastFlat = false,
        long outputDelay = 0
    )
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (fastPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        if (outputDelay < 0 || outputDelay > long.MaxValue - (publishStartup ? 0L : period - 1L))
            throw new ArgumentOutOfRangeException(nameof(outputDelay));
        Period = period;
        FastPeriod = fastPeriod;
        SlowPeriod = slowPeriod;
        PublishStartup = publishStartup;
        ClampFastPeriod = clampFastPeriod;
        ResetFlat = resetFlat;
        FastFlat = fastFlat;
        OutputDelay = outputDelay;
    }

    /// <summary>Number of changes in the efficiency window.</summary>
    public int Period { get; }

    /// <summary>Fast rate period.</summary>
    public int FastPeriod { get; }

    /// <summary>Slow rate period.</summary>
    public int SlowPeriod { get; }

    /// <summary>Whether closes are published before the seed.</summary>
    public bool PublishStartup { get; }

    /// <summary>Whether fast period is capped at Period.</summary>
    public bool ClampFastPeriod { get; }

    /// <summary>Whether a flat window resets the average to close.</summary>
    public bool ResetFlat { get; }

    /// <summary>Whether flat-window efficiency is one instead of zero. ResetFlat still overrides the recurrence.</summary>
    public bool FastFlat { get; }

    /// <summary>Additional bars before average publication; efficiency publication is unchanged.</summary>
    public long OutputDelay { get; }

    internal long FirstOutputIndex => (PublishStartup ? 0L : Period - 1L) + OutputDelay;

    /// <summary>Average startup index, capped at Int32.MaxValue for the indicator metadata interface.</summary>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, FirstOutputIndex);

    /// <summary>Adaptive average, or zero when absent.</summary>
    public IIndicatorOutput Average => Outputs[0];

    /// <summary>Efficiency ratio, or zero when absent.</summary>
    public IIndicatorOutput Efficiency => Outputs[1];

    /// <summary>Average presence.</summary>
    public IIndicatorOutput AverageIsDefined => Outputs[2];

    /// <summary>Efficiency presence.</summary>
    public IIndicatorOutput EfficiencyIsDefined => Outputs[3];

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
                        SeededAdaptiveReference
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

    private sealed class State(SeededAdaptiveAverage owner) : IMultiOutputState, IWideAverageState
    {
        private static readonly BigInteger Grid = BigInteger.One << 1074;
        private readonly BigInteger _fast = RocBankValue.RoundUnits(
            2 * Grid,
            (long)(
                owner.ClampFastPeriod ? Math.Min(owner.Period, owner.FastPeriod) : owner.FastPeriod
            ) + 1
        );
        private readonly BigInteger _slow = RocBankValue.RoundUnits(
            2 * Grid,
            (long)owner.SlowPeriod + 1
        );
        private readonly Queue<BigInteger> _prices = new(),
            _changes = new();
        private BigInteger _volatility,
            _previous,
            _average;
        private long _index = -1;
        public BigInteger RoundedUnits => _average;

        public void Reset()
        {
            _prices.Clear();
            _changes.Clear();
            _volatility = _previous = _average = 0;
            _index = -1;
        }

        public void Update(in Bar bar, Span<double> output) =>
            UpdateUnits(ExactVarianceWindow.Units(bar.Close), output);

        public void UpdateUnits(BigInteger price, Span<double> output)
        {
            output.Clear();
            _index++;
            if (_index > 0)
            {
                var change = BigInteger.Abs(price - _previous);
                if (_changes.Count == owner.Period)
                    _volatility -= _changes.Dequeue();
                _changes.Enqueue(change);
                _volatility += change;
            }
            _previous = price;
            if (_prices.Count == owner.Period)
            {
                var old = _prices.Dequeue();
                var efficiency = _volatility.IsZero
                    ? owner.FastFlat
                        ? Grid
                        : BigInteger.Zero
                    : RocBankValue.RoundUnits(BigInteger.Abs(price - old) * Grid, _volatility);
                var rate = RocBankValue.RoundUnits(
                    efficiency * (_fast - _slow) + _slow * Grid,
                    Grid
                );
                var square = RocBankValue.RoundUnits(rate * rate, Grid);
                _average =
                    owner.ResetFlat && _volatility.IsZero
                        ? price
                        : RocBankValue.RoundUnits(
                            _average * (Grid - square) + price * square,
                            Grid
                        );
                output[1] = ExactMeanAccumulator.UnitRatio(efficiency, 1);
                output[3] = 1;
            }
            else
                _average = price;
            _prices.Enqueue(price);
            if (_index >= owner.FirstOutputIndex)
            {
                output[0] = ExactMeanAccumulator.UnitRatio(_average, 1);
                output[2] = 1;
            }
        }
    }
}
