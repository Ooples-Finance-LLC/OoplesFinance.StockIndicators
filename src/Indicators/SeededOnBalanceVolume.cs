using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Initial value for on-balance volume.</summary>
public enum ObvSeed
{
    /// <summary>Start at zero, ignoring the first volume.</summary>
    Zero,

    /// <summary>Start at the first volume regardless of the initial price.</summary>
    FirstVolume,
}

/// <summary>Exact cumulative on-balance volume with an explicit seed and optional simple average.</summary>
/// <remarks>Higher closes add volume, lower closes subtract it, and equal closes leave the total unchanged.
/// The average excludes the initial seed observation: its first window is bars 1 through AveragePeriod.
/// Unavailable averages have a zero placeholder and a zero validity flag. The runtime rejects true total overflow.</remarks>
public sealed class SeededOnBalanceVolume
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates OBV with a seed and an optional positive averaging period.</summary>
    public SeededOnBalanceVolume(ObvSeed seed = ObvSeed.FirstVolume, int? averagePeriod = null)
        : base(3)
    {
        if (!Enum.IsDefined(typeof(ObvSeed), seed))
            throw new ArgumentOutOfRangeException(nameof(seed));
        if (averagePeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(averagePeriod));
        Seed = seed;
        AveragePeriod = averagePeriod;
        (Value, Average, AverageIsDefined) = DeclaredOutputs;
    }

    /// <summary>Initial OBV convention.</summary>
    public ObvSeed Seed { get; }

    /// <summary>Number of observations after the seed to average, or null to disable.</summary>
    public int? AveragePeriod { get; }

    /// <summary>On-balance volume, available from the first bar.</summary>
    public IIndicatorOutput Value { get; }

    /// <summary>Mean of rounded OBV outputs, or zero when unavailable.</summary>
    public IIndicatorOutput Average { get; }

    /// <summary>One when Average is defined; otherwise zero.</summary>
    public IIndicatorOutput AverageIsDefined { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Seed, AveragePeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => Reference(bars)[0],
                IndicatorErrorBudget.Exact
            ),
            IndicatorValidationRule.Reference(1, bars => Reference(bars)[1], 0, 0),
            IndicatorValidationRule.Reference(2, bars => Reference(bars)[2], 0, 0),
        ];

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = Enumerable.Range(0, 3).Select(_ => new double[bars.Count]).ToArray();
        var total = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var direction =
                i == 0
                    ? (Seed == ObvSeed.FirstVolume ? 1 : 0)
                    : bars[i].Close.CompareTo(bars[i - 1].Close);
            total +=
                ReferenceFraction.FromDouble(bars[i].Volume) * new ReferenceFraction(direction);
            result[0][i] = total.ToDouble();
            if (double.IsInfinity(result[0][i]))
                break;
            if (AveragePeriod is not int period || i < period)
                continue;
            var sum = new ReferenceFraction(0);
            for (var j = i - period + 1; j <= i; j++)
                sum += ReferenceFraction.FromDouble(result[0][j]);
            result[1][i] = (sum / new ReferenceFraction(period)).ToDouble();
            result[2][i] = 1;
        }
        return result;
    }

    private sealed class State(ObvSeed seed, int? averagePeriod) : IMultiOutputState
    {
        private bool _initialized;
        private double _previous;
        private ExactMeanAccumulator _total,
            _mean;
        private readonly Queue<double> _history = new();

        public void Reset()
        {
            _initialized = false;
            _previous = 0;
            _total = _mean = default;
            _history.Clear();
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            if (!_initialized)
            {
                if (seed == ObvSeed.FirstVolume)
                    _total.Add(bar.Volume);
                _initialized = true;
                _previous = bar.Close;
                outputs[0] = _total.Mean(1);
                return;
            }
            if (bar.Close > _previous)
                _total.Add(bar.Volume);
            else if (bar.Close < _previous)
                _total.Add(bar.Volume, -1);
            _previous = bar.Close;
            outputs[0] = _total.Mean(1);
            if (averagePeriod is not int period || double.IsInfinity(outputs[0]))
                return;
            if (_history.Count == period)
                _mean.Add(_history.Dequeue(), -1);
            _history.Enqueue(outputs[0]);
            _mean.Add(outputs[0]);
            if (_history.Count != period)
                return;
            outputs[1] = _mean.Mean(period);
            outputs[2] = 1;
        }
    }
}
