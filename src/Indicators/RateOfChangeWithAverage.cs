using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Momentum and percentage change, with an optional simple average and explicit validity flags.</summary>
/// <remarks>Startup and unavailable outputs have zero placeholders. A zero earlier close makes ROC
/// unavailable; any unavailable ROC in the averaging window makes its average unavailable.
/// Momentum and ROC round once. The average rounds the exact mean of the published, rounded ROC values.
/// The runtime rejects unrepresentable published momentum or ROC. Storage grows only with observed history.</remarks>
public sealed class RateOfChangeWithAverage
    : MultiOutputIndicatorBase,
        IMomentumIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a lagged return with optional averaging. Null disables the average.</summary>
    public RateOfChangeWithAverage(int period = 12, int? averagePeriod = null)
        : base(5)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (averagePeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(averagePeriod));
        Period = period;
        AveragePeriod = averagePeriod;
        (Momentum, Roc, RocIsDefined, Average, AverageIsDefined) = DeclaredOutputs;
    }

    /// <summary>Number of bars separating closes.</summary>
    public int Period { get; }

    /// <summary>Number of consecutive ROC values to average, or null to disable.</summary>
    public int? AveragePeriod { get; }

    /// <summary>Current close minus the earlier close; zero during startup.</summary>
    public IIndicatorOutput Momentum { get; }

    /// <summary>Percentage change, or zero when RocIsDefined is zero.</summary>
    public IIndicatorOutput Roc { get; }

    /// <summary>One when ROC is defined; otherwise zero.</summary>
    public IIndicatorOutput RocIsDefined { get; }

    /// <summary>Mean ROC, or zero when AverageIsDefined is zero.</summary>
    public IIndicatorOutput Average { get; }

    /// <summary>One when a complete averaging window contains only defined returns.</summary>
    public IIndicatorOutput AverageIsDefined { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period;

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Roc;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, AveragePeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => Reference(bars, 0),
                IndicatorErrorBudget.Exact
            ),
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                1,
                bars => Reference(bars, 1),
                IndicatorErrorBudget.Exact
            ),
            IndicatorValidationRule.Reference(2, bars => Reference(bars, 2), 0, 0),
            IndicatorValidationRule.Reference(3, bars => Reference(bars, 3), 0, 0),
            IndicatorValidationRule.Reference(4, bars => Reference(bars, 4), 0, 0),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars, int slot)
    {
        var values = new double[bars.Count];
        for (var i = Period; i < bars.Count; i++)
        {
            var previous = ReferenceFraction.FromDouble(bars[i - Period].Close);
            var difference = ReferenceFraction.FromDouble(bars[i].Close) - previous;
            if (slot == 0)
                values[i] = difference.ToDouble();
            else if (slot < 3 && bars[i - Period].Close != 0)
                values[i] =
                    slot == 2 ? 1 : (difference / previous * new ReferenceFraction(100)).ToDouble();
        }
        if (slot < 3 || AveragePeriod is not int window)
            return values;
        for (long i = (long)Period + window - 1; i < bars.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            var available = true;
            for (var j = (int)i - window + 1; j <= i; j++)
            {
                if (bars[j - Period].Close == 0)
                {
                    available = false;
                    break;
                }
                var previous = ReferenceFraction.FromDouble(bars[j - Period].Close);
                var change = (
                    (ReferenceFraction.FromDouble(bars[j].Close) - previous)
                    / previous
                    * new ReferenceFraction(100)
                ).ToDouble();
                // A published ROC overflow rejects execution before the average can be used.
                if (double.IsInfinity(change))
                {
                    available = false;
                    break;
                }
                sum += ReferenceFraction.FromDouble(change);
            }
            if (available)
                values[(int)i] = slot == 4 ? 1 : (sum / new ReferenceFraction(window)).ToDouble();
        }
        return values;
    }

    private sealed class State(int period, int? averagePeriod) : IMultiOutputState
    {
        private readonly Queue<double> _prices = new();
        private readonly Queue<(double Value, bool Present)> _returns = new();
        private ExactMeanAccumulator _sum;
        private int _missing;

        public void Reset()
        {
            _prices.Clear();
            _returns.Clear();
            _sum = default;
            _missing = 0;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            if (_prices.Count < period)
            {
                _prices.Enqueue(bar.Close);
                return;
            }
            var previous = _prices.Dequeue();
            _prices.Enqueue(bar.Close);
            var difference = new ExactMeanAccumulator();
            difference.Add(bar.Close);
            difference.Add(previous, -1);
            outputs[0] = difference.Mean(1);
            var present = previous != 0;
            var roc = present ? RoundedPercentageChange.Of(bar.Close, previous) : 0;
            outputs[1] = roc;
            outputs[2] = present ? 1 : 0;
            if (averagePeriod is not int window || double.IsInfinity(roc))
                return;
            if (_returns.Count == window)
            {
                var old = _returns.Dequeue();
                if (old.Present)
                    _sum.Add(old.Value, -1);
                else
                    _missing--;
            }
            _returns.Enqueue((roc, present));
            if (present)
                _sum.Add(roc);
            else
                _missing++;
            if (_returns.Count == window && _missing == 0)
            {
                outputs[3] = _sum.Mean(window);
                outputs[4] = 1;
            }
        }
    }
}
