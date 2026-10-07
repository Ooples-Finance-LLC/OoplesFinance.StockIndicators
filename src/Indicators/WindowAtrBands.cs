using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Centerline conventions for ATR envelopes.</summary>
public enum AtrBandCenterMode
{
    /// <summary>Full-window SMA, published as soon as its own window is complete.</summary>
    Simple,

    /// <summary>SMA-seeded EMA, published after both period windows are complete.</summary>
    MeanSeededExponential,

    /// <summary>First-close-seeded EMA, published after both period windows are complete.</summary>
    FirstSeededExponential,
}

/// <summary>Keltner or STARC envelopes with explicit centerline and width conventions.</summary>
/// <remarks>Mean-seeded Wilder ATR starts at index atrPeriod. Exponential centers
/// wait until max(centerPeriod,atrPeriod)-1; simple centers start at centerPeriod-1.
/// Centers and ATR stages round once, with extended ATR upper exponents. Each band
/// rounds once from center plus/minus the complete ATR offset. Optional width is
/// the exact difference of published bands divided by center, rounded once, and
/// is absent for a zero center. Histories grow lazily.</remarks>
public sealed class WindowAtrBands : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates an ATR envelope with positive periods and a finite multiplier.</summary>
    public WindowAtrBands(
        int centerPeriod = 20,
        int atrPeriod = 10,
        double multiplier = 2,
        AtrBandCenterMode centerMode = AtrBandCenterMode.MeanSeededExponential,
        bool includeWidth = true
    )
        : base(8)
    {
        if (centerPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(centerPeriod));
        if (atrPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(atrPeriod));
        if (!double.IsFinite(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (
            centerMode < AtrBandCenterMode.Simple
            || centerMode > AtrBandCenterMode.FirstSeededExponential
        )
            throw new ArgumentOutOfRangeException(nameof(centerMode));
        CenterPeriod = centerPeriod;
        AtrPeriod = atrPeriod;
        Multiplier = multiplier;
        CenterMode = centerMode;
        IncludeWidth = includeWidth;
    }

    /// <summary>Centerline period.</summary>
    public int CenterPeriod { get; }

    /// <summary>Wilder ATR period.</summary>
    public int AtrPeriod { get; }

    /// <summary>ATR offset multiplier; zero and negative values are supported.</summary>
    public double Multiplier { get; }

    /// <summary>Centerline initialization and publication rule.</summary>
    public AtrBandCenterMode CenterMode { get; }

    /// <summary>Whether normalized width is evaluated and published.</summary>
    public bool IncludeWidth { get; }

    /// <summary>Centerline or zero placeholder.</summary>
    public IIndicatorOutput Center => Outputs[0];

    /// <summary>Upper band or zero placeholder.</summary>
    public IIndicatorOutput Upper => Outputs[1];

    /// <summary>Lower band or zero placeholder.</summary>
    public IIndicatorOutput Lower => Outputs[2];

    /// <summary>Normalized width or zero placeholder.</summary>
    public IIndicatorOutput Width => Outputs[3];

    /// <summary>Centerline presence.</summary>
    public IIndicatorOutput CenterIsDefined => Outputs[4];

    /// <summary>Upper-band presence.</summary>
    public IIndicatorOutput UpperIsDefined => Outputs[5];

    /// <summary>Lower-band presence.</summary>
    public IIndicatorOutput LowerIsDefined => Outputs[6];

    /// <summary>Width presence.</summary>
    public IIndicatorOutput WidthIsDefined => Outputs[7];

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(CenterPeriod, AtrPeriod, Multiplier, CenterMode, IncludeWidth);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 8)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        AtrBandsReference.Calculate(
                            bars,
                            CenterPeriod,
                            AtrPeriod,
                            Multiplier,
                            CenterMode,
                            IncludeWidth
                        )[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(
        int centerPeriod,
        int atrPeriod,
        double multiplier,
        AtrBandCenterMode mode,
        bool width
    ) : IMultiOutputState
    {
        private readonly SeededAtrWindow _atr = new(atrPeriod);
        private readonly Queue<double> _window = new();
        private ExactMeanAccumulator _sum;
        private int _seedCount,
            _count;
        private double _center;
        private bool _started;

        public void Reset()
        {
            _atr.Reset();
            _window.Clear();
            _sum = default;
            _seedCount = _count = 0;
            _center = 0;
            _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _atr.Add(bar);
            if (_count < Math.Max(centerPeriod, atrPeriod))
                _count++;
            if (mode == AtrBandCenterMode.Simple)
            {
                if (_window.Count == centerPeriod)
                    _sum.Add(_window.Dequeue(), -1);
                _window.Enqueue(bar.Close);
                _sum.Add(bar.Close);
                if (_window.Count < centerPeriod)
                    return;
                _center = _sum.Mean(centerPeriod);
            }
            else if (mode == AtrBandCenterMode.MeanSeededExponential && _seedCount < centerPeriod)
            {
                _sum.Add(bar.Close);
                if (++_seedCount < centerPeriod)
                    return;
                _center = _sum.Mean(centerPeriod);
                _sum = default;
            }
            else if (mode == AtrBandCenterMode.FirstSeededExponential && !_started)
            {
                _center = bar.Close;
                _started = true;
            }
            else
            {
                var sum = new ExactMeanAccumulator();
                sum.Add(bar.Close, 2);
                sum.Add(_center, centerPeriod - 1);
                _center = sum.Mean((long)centerPeriod + 1);
            }
            if (mode != AtrBandCenterMode.Simple && _count < Math.Max(centerPeriod, atrPeriod))
                return;
            output[0] = _center;
            output[4] = 1;
            if (!_atr.HasAverage)
                return;
            var offset = new ExactMeanAccumulator();
            offset.AddProduct(_atr.Average.Mantissa, multiplier);
            offset.ScaleByPowerOfTwo(_atr.Average.UpperShift);
            var upper = offset;
            upper.Add(_center);
            var lower = new ExactMeanAccumulator();
            lower.Add(_center);
            lower.Subtract(offset);
            output[1] = upper.Mean(1);
            output[2] = lower.Mean(1);
            output[5] = output[6] = 1;
            if (
                !width
                || _center == 0 // NOSONAR: Width is undefined only at an exactly zero center.
                || !double.IsFinite(output[1])
                || !double.IsFinite(output[2])
            )
                return;
            var numerator = new ExactMeanAccumulator();
            numerator.Add(output[1]);
            numerator.Add(output[2], -1);
            var denominator = new ExactMeanAccumulator();
            denominator.Add(_center);
            output[3] = numerator.Ratio(denominator);
            output[7] = 1;
        }
    }
}
