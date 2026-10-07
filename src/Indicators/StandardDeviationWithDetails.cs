using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window population deviation, mean, Z-score, optional deviation SMA, and presence flags.</summary>
/// <remarks>Deviation and mean start with the first complete window. Z-score is absent
/// exactly when that window has zero variance; subnormal normalization uses unrounded
/// moments even when the published deviation rounds to zero. Optional smoothing averages
/// the published deviations and starts only after its own complete window. Missing values
/// use zero placeholders. Both histories grow lazily; no derived period sum is allocated.</remarks>
public sealed class StandardDeviationWithDetails
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates population deviation statistics and optional smoothing.</summary>
    /// <param name="period">At least two observations in the price window.</param>
    /// <param name="smoothingPeriod">Positive deviation SMA period, or null to leave that output absent.</param>
    public StandardDeviationWithDetails(int period = 20, int? smoothingPeriod = null)
        : base(8)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (smoothingPeriod is <= 0)
            throw new ArgumentOutOfRangeException(nameof(smoothingPeriod));
        Period = period;
        SmoothingPeriod = smoothingPeriod;
    }

    /// <summary>Number of prices required for deviation, mean, and Z-score.</summary>
    public int Period { get; }

    /// <summary>Number of published deviations in the optional SMA, or null when disabled.</summary>
    public int? SmoothingPeriod { get; }

    /// <summary>Population deviation of the complete price window.</summary>
    public IIndicatorOutput Deviation => Outputs[0];

    /// <summary>Mean close of the complete price window.</summary>
    public IIndicatorOutput Mean => Outputs[1];

    /// <summary>Current close's normalized distance from its window's mean.</summary>
    public IIndicatorOutput ZScore => Outputs[2];

    /// <summary>Optional mean of the last smoothingPeriod published deviations.</summary>
    public IIndicatorOutput SmoothedDeviation => Outputs[3];

    /// <summary>One when the price window is complete, otherwise zero.</summary>
    public IIndicatorOutput DeviationIsDefined => Outputs[4];

    /// <summary>One when the price window is complete, otherwise zero.</summary>
    public IIndicatorOutput MeanIsDefined => Outputs[5];

    /// <summary>One when the price window is complete and its exact variance is nonzero.</summary>
    public IIndicatorOutput ZScoreIsDefined => Outputs[6];

    /// <summary>One when smoothing is enabled and its deviation window is complete.</summary>
    public IIndicatorOutput SmoothedDeviationIsDefined => Outputs[7];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Deviation;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, SmoothingPeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 8)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = Enumerable.Range(0, 8).Select(_ => new double[bars.Count]).ToArray();
        for (var i = Period - 1; i < bars.Count; i++)
        {
            var window = bars.Skip(i - Period + 1)
                .Take(Period)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var n = new ReferenceFraction(Period);
            var mean = window.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
            var variance =
                window.Aggregate(new ReferenceFraction(0), (a, b) => a + (b - mean) * (b - mean))
                / n;
            result[0][i] = variance.SqrtToDouble();
            result[1][i] = mean.ToDouble();
            result[4][i] = result[5][i] = 1;
            if (variance.Sign > 0)
            {
                var delta = window[^1] - mean;
                result[2][i] = delta.Sign * (delta * delta / variance).SqrtToDouble();
                result[6][i] = 1;
            }
            if (SmoothingPeriod is int smooth && (long)i >= (long)Period + smooth - 2)
            {
                var sum = new ReferenceFraction(0);
                for (var j = i - smooth + 1; j <= i; j++)
                    sum += ReferenceFraction.FromDouble(result[0][j]);
                result[3][i] = (sum / new ReferenceFraction(smooth)).ToDouble();
                result[7][i] = 1;
            }
        }
        return result;
    }

    private sealed class State(int period, int? smoothingPeriod) : IMultiOutputState
    {
        private readonly WindowDispersion.State _statistics = new(
            period,
            WindowDispersionOutput.StandardDeviation,
            false,
            1
        );
        private readonly Queue<double>? _smoothing = smoothingPeriod.HasValue ? new() : null;
        private ExactMeanAccumulator _smoothSum;

        public void Reset()
        {
            _statistics.Reset();
            _smoothing?.Clear();
            _smoothSum = default;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            var deviation = _statistics.Update(bar);
            if (_statistics.Count < period)
                return;
            outputs[0] = deviation;
            outputs[1] = _statistics.Mean;
            outputs[4] = outputs[5] = 1;
            if (_statistics.HasVariance)
            {
                outputs[2] = _statistics.Reading(WindowDispersionOutput.ZScore);
                outputs[6] = 1;
            }
            if (_smoothing is null)
                return;
            if (_smoothing.Count == smoothingPeriod)
                _smoothSum.Add(_smoothing.Dequeue(), -1);
            _smoothing.Enqueue(deviation);
            _smoothSum.Add(deviation);
            if (_smoothing.Count != smoothingPeriod)
                return;
            outputs[3] = _smoothSum.Mean(_smoothing.Count);
            outputs[7] = 1;
        }
    }
}
