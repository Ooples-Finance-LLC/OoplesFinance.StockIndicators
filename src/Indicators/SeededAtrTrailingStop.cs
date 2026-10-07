using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Price basis for a seeded ATR trailing stop.</summary>
public enum AtrTrailBasis
{
    /// <summary>Both candidate bands surround close.</summary>
    Close,

    /// <summary>Upper candidates surround high; lower candidates surround low.</summary>
    HighLow,

    /// <summary>Both candidates surround the high/low midpoint (SuperTrend).</summary>
    Midpoint,
}

/// <summary>Mean-seeded Wilder ATR trailing bands with exact reversal and tie comparisons.</summary>
/// <remarks>Outputs begin at index period. A close equal to the active band selects the upper
/// band and a bearish trend. The upper and lower outputs are mutually exclusive. Midpoint,
/// ATR width, candidate bands, and ATR recurrences round once with extended upper exponents.
/// Only published stop values must fit binary64; unpublished wide bands remain comparable.</remarks>
public sealed class SeededAtrTrailingStop : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a stop with period at least two and a finite positive multiplier.</summary>
    public SeededAtrTrailingStop(
        int period = 14,
        double multiplier = 3,
        AtrTrailBasis basis = AtrTrailBasis.Close
    )
        : base(6)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(multiplier) || multiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (basis is < AtrTrailBasis.Close or > AtrTrailBasis.Midpoint)
            throw new ArgumentOutOfRangeException(nameof(basis));
        Period = period;
        Multiplier = multiplier;
        Basis = basis;
    }

    /// <summary>ATR seed and smoothing period.</summary>
    public int Period { get; }

    /// <summary>ATR width multiplier.</summary>
    public double Multiplier { get; }

    /// <summary>Candidate band price basis.</summary>
    public AtrTrailBasis Basis { get; }

    /// <summary>Active trailing stop.</summary>
    public IIndicatorOutput Stop => Outputs[0];

    /// <summary>Upper stop while bearish.</summary>
    public IIndicatorOutput UpperBand => Outputs[1];

    /// <summary>Lower stop while bullish.</summary>
    public IIndicatorOutput LowerBand => Outputs[2];

    /// <summary>Stop presence.</summary>
    public IIndicatorOutput IsStopDefined => Outputs[3];

    /// <summary>Upper stop presence.</summary>
    public IIndicatorOutput IsUpperBandDefined => Outputs[4];

    /// <summary>Lower stop presence.</summary>
    public IIndicatorOutput IsLowerBandDefined => Outputs[5];

    /// <inheritdoc/>
    public override int WarmupBars => Period;

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
                        AtrTrailingReference
                            .Values(bars, Period, Multiplier, Basis)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(SeededAtrTrailingStop owner) : IMultiOutputState
    {
        private readonly SeededAtrWindow _atr = new(owner.Period);
        private BigInteger _upper,
            _lower,
            _previous;
        private bool _ready,
            _bullish;
        private static readonly BigInteger Grid = BigInteger.One << 1074;

        public void Reset()
        {
            _atr.Reset();
            _upper = _lower = _previous = 0;
            _ready = _bullish = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _atr.Add(bar);
            var close = ExactVarianceWindow.Units(bar.Close);
            if (_atr.HasAverage)
            {
                var high = ExactVarianceWindow.Units(bar.High);
                var low = ExactVarianceWindow.Units(bar.Low);
                var midpoint = RocBankValue.RoundUnits(high + low, 2);
                var upperBasis =
                    owner.Basis == AtrTrailBasis.Close ? close
                    : owner.Basis == AtrTrailBasis.HighLow ? high
                    : midpoint;
                var lowerBasis =
                    owner.Basis == AtrTrailBasis.Close ? close
                    : owner.Basis == AtrTrailBasis.HighLow ? low
                    : midpoint;
                var average =
                    ExactVarianceWindow.Units(_atr.Average.Mantissa) << _atr.Average.UpperShift;
                var width = RocBankValue.RoundUnits(
                    average * ExactVarianceWindow.Units(owner.Multiplier),
                    Grid
                );
                var upper = RocBankValue.RoundUnits(upperBasis + width, 1);
                var lower = RocBankValue.RoundUnits(lowerBasis - width, 1);
                if (!_ready)
                {
                    _ready = true;
                    _bullish =
                        close >= (owner.Basis == AtrTrailBasis.Midpoint ? midpoint : _previous);
                    _upper = upper;
                    _lower = lower;
                }
                if (upper < _upper || _previous > _upper)
                    _upper = upper;
                if (lower > _lower || _previous < _lower)
                    _lower = lower;
                _bullish = close > (_bullish ? _lower : _upper);
                var slot = _bullish ? 2 : 1;
                output[0] = output[slot] = ExactMeanAccumulator.UnitRatio(
                    _bullish ? _lower : _upper,
                    1
                );
                output[3] = output[slot + 3] = 1;
            }
            _previous = close;
        }
    }
}
