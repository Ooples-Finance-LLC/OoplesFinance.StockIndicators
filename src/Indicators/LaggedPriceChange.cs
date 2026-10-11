using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>The formula used to compare the current close with an earlier close.</summary>
public enum PriceChangeKind
{
    /// <summary>Current minus previous.</summary>
    Difference,

    /// <summary>(Current minus previous) divided by previous.</summary>
    Fraction,

    /// <summary>One hundred times the fractional change.</summary>
    Percent,

    /// <summary>Current divided by previous.</summary>
    Ratio,

    /// <summary>One hundred times the price ratio.</summary>
    RatioPercent,

    /// <summary>Positive part of current minus previous.</summary>
    Gain,

    /// <summary>Positive part of previous minus current.</summary>
    Loss,
}

/// <summary>Compares closes separated by a fixed number of bars.</summary>
/// <remarks>Incomplete history returns zero. Ratio forms return zero when the earlier close is zero.
/// Arithmetic rounds the complete formula once; the runtime rejects an unrepresentable result.
/// Storage grows only with observed history, up to the requested period.</remarks>
public sealed class LaggedPriceChange
    : IndicatorBase,
        IMomentumIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a lagged close comparison with the selected formula.</summary>
    public LaggedPriceChange(int period = 10, PriceChangeKind kind = PriceChangeKind.Difference)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(typeof(PriceChangeKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Period = period;
        Kind = kind;
    }

    /// <summary>Number of bars between the two closes.</summary>
    public int Period { get; }

    /// <summary>Formula applied to the two closes.</summary>
    public PriceChangeKind Kind { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Kind);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                new IndicatorErrorBudget(0, 0)
            ),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var values = new double[bars.Count];
        for (var i = Period; i < bars.Count; i++)
        {
            var current = ReferenceFraction.FromDouble(bars[i].Close);
            var previous = ReferenceFraction.FromDouble(bars[i - Period].Close);
            if (Kind is PriceChangeKind.Gain or PriceChangeKind.Loss)
            {
                var change = Kind == PriceChangeKind.Gain ? current - previous : previous - current;
                values[i] = Math.Max(0, change.ToDouble());
                continue;
            }
            if (Kind != PriceChangeKind.Difference && bars[i - Period].Close == 0)
                continue;
            values[i] = (
                Kind switch
                {
                    PriceChangeKind.Difference => current - previous,
                    PriceChangeKind.Fraction => (current - previous) / previous,
                    PriceChangeKind.Percent => (current - previous)
                        / previous
                        * new ReferenceFraction(100),
                    PriceChangeKind.Ratio => current / previous,
                    _ => current / previous * new ReferenceFraction(100),
                }
            ).ToDouble();
        }
        return values;
    }

    private sealed class State(int period, PriceChangeKind kind) : IIndicatorState
    {
        private readonly Queue<double> _history = new();

        public void Reset() => _history.Clear();

        public double Update(in Bar bar)
        {
            if (_history.Count < period)
            {
                _history.Enqueue(bar.Close);
                return 0;
            }
            var previous = _history.Dequeue();
            _history.Enqueue(bar.Close);
            return Calculate(bar.Close, previous, kind);
        }
    }

    internal static double Calculate(double current, double previous, PriceChangeKind kind)
    {
#if !NETFRAMEWORK
        // A difference or quotient of two finite doubles already rounds the
        // complete formula once. Exact cancellation has canonical positive zero.
        if (kind == PriceChangeKind.Difference)
            return current == previous ? 0 : current - previous; // NOSONAR: S1244 - Exact equality defines canonical zero.
        if (kind == PriceChangeKind.Gain) return current <= previous ? 0 : current - previous;
        if (kind == PriceChangeKind.Loss) return current >= previous ? 0 : previous - current;
        if (kind == PriceChangeKind.Ratio) return current == 0 || previous == 0 ? 0 : current / previous;
        if (kind == PriceChangeKind.Fraction && previous != 0
            && Binary64ArithmeticCertificate.TryDifference(current, previous, out var change))
            return change == 0 ? 0 : change / previous;
        if (kind is PriceChangeKind.Percent or PriceChangeKind.RatioPercent && previous != 0)
        {
            double value = current;
            bool exact = kind == PriceChangeKind.RatioPercent
                || Binary64ArithmeticCertificate.TryDifference(current, previous, out value);
            if (exact && Binary64ArithmeticCertificate.TryProduct(value, 100, out var scaled))
                return scaled / previous;
        }
#endif
        if (kind is PriceChangeKind.Gain or PriceChangeKind.Loss)
        {
            var positive = kind == PriceChangeKind.Gain;
            if (positive ? current <= previous : current >= previous)
                return 0;
            var difference = new ExactMeanAccumulator();
            difference.Add(current, positive ? 1 : -1);
            difference.Add(previous, positive ? -1 : 1);
            return difference.Mean(1);
        }
        if (kind != PriceChangeKind.Difference && previous == 0)
            return 0;
        var scale = kind is PriceChangeKind.Percent or PriceChangeKind.RatioPercent ? 100 : 1;
        var numerator = new ExactMeanAccumulator();
        numerator.Add(current, scale);
        if (
            kind
            is PriceChangeKind.Difference
                or PriceChangeKind.Fraction
                or PriceChangeKind.Percent
        )
            numerator.Add(previous, -scale);
        if (kind == PriceChangeKind.Difference)
            return numerator.Mean(1);
        var denominator = new ExactMeanAccumulator();
        denominator.Add(previous);
        return numerator.Ratio(denominator);
    }
}
