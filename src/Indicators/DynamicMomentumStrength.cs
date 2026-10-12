using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Volatility-selected fixed-period Wilder RSI with explicit output presence.</summary>
/// <remarks>Each selected period uses the complete original history, matching
/// DynamicMomentumSnapshot. Period states are created lazily and retain no output
/// trajectories. A close-price history supports periods first selected later.</remarks>
public sealed class DynamicMomentumStrength : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive deviation, smoothing and RSI periods with ordered positive limits.</summary>
    public DynamicMomentumStrength(int deviationPeriod = 5, int smoothingPeriod = 10,
        int rsiPeriod = 14, int upperLimit = 30, int lowerLimit = 5) : base(2)
    {
        if (deviationPeriod < 1 || smoothingPeriod < 1 || rsiPeriod < 1 || lowerLimit < 1 || upperLimit < lowerLimit)
            throw new ArgumentOutOfRangeException(nameof(deviationPeriod));
        DeviationPeriod = deviationPeriod; SmoothingPeriod = smoothingPeriod;
        RsiPeriod = rsiPeriod; UpperLimit = upperLimit; LowerLimit = lowerLimit;
    }
    /// <summary>Population deviation window.</summary>
    public int DeviationPeriod { get; }
    /// <summary>Deviation smoothing window.</summary>
    public int SmoothingPeriod { get; }
    /// <summary>Baseline RSI period.</summary>
    public int RsiPeriod { get; }
    /// <summary>Largest selected period.</summary>
    public int UpperLimit { get; }
    /// <summary>Smallest selected period.</summary>
    public int LowerLimit { get; }
    /// <summary>Selected RSI, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];
    /// <summary>One when the selected RSI is present.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];
    /// <inheritdoc/>
    protected internal override object CreateState() => new DynamicMomentumStrengthState(
        DeviationPeriod, SmoothingPeriod, RsiPeriod, UpperLimit, LowerLimit);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 2).Select(slot =>
        IndicatorValidationRule.ReferenceWithOverflowRejection(slot, bars =>
            DynamicMomentumSnapshot.FromValues(bars.Select(b => (double?)b.Close).ToArray(),
                DeviationPeriod, SmoothingPeriod, RsiPeriod, UpperLimit, LowerLimit)
                .Select(value => slot == 0 ? value ?? 0 : value.HasValue ? 1d : 0).ToArray(),
            IndicatorErrorBudget.Exact));
}

internal sealed class DynamicMomentumStrengthState(int deviationPeriod, int smoothingPeriod,
    int rsiPeriod, int upperLimit, int lowerLimit) : IMultiOutputState
{
    private readonly List<double> _prices = new();
    private readonly Queue<double> _deviations = new();
    private readonly Dictionary<int, Trajectory> _trajectories = new();
    private JurikDyadic _sum, _squares, _smoothSum;

    private sealed class Trajectory(int period)
    {
        internal readonly NullableStrengthOscillator.Calculator Calculator =
            new(period, NullableStrengthConvention.RelativeStrengthIndex, 1);
        internal int Consumed;
    }

    public void Reset()
    {
        _prices.Clear(); _deviations.Clear();
        _sum = _squares = _smoothSum = default;
        foreach (var trajectory in _trajectories.Values)
        { trajectory.Calculator.Reset(); trajectory.Consumed = 0; }
    }

    private void Moment(double price, bool add)
    {
        var value = JurikDyadic.FromDouble(price);
        if (add) _sum.AddExact(value); else _sum.Subtract(value);
        value.Square();
        if (add) _squares.AddExact(value); else _squares.Subtract(value);
    }

    public void Update(in Bar bar, Span<double> output)
    {
        if (!FrameworkCompatibility.IsFinite(bar.Close)) throw new ArgumentOutOfRangeException(nameof(bar));
        output.Clear();
        _prices.Add(bar.Close);
        int count = _prices.Count;
        Moment(bar.Close, true);
        if (count > deviationPeriod) Moment(_prices[count - deviationPeriod - 1], false);
        if (count < deviationPeriod) return;

        var radicand = _squares;
        radicand.Multiply(deviationPeriod);
        var squareOfSum = _sum;
        squareOfSum.Square();
        radicand.Subtract(squareOfSum);
        double deviation = radicand.SqrtMean((long)deviationPeriod * deviationPeriod);
        if (_deviations.Count == smoothingPeriod)
            _smoothSum.Subtract(JurikDyadic.FromDouble(_deviations.Dequeue()));
        _deviations.Enqueue(deviation);
        _smoothSum.Add(deviation);
        // Calendar startup is independent of deviation startup. Absent early
        // deviations are omitted from the smoothing divisor, as in the snapshot.
        if (count < smoothingPeriod) return;
        double mean = _smoothSum.Mean(_deviations.Count);
        if (mean == 0) return;
        double ratio = deviation / mean;
        if (ratio == 0) return;
        int selected = double.IsPositiveInfinity(ratio) ? lowerLimit
            : DynamicMomentumPeriod.SelectExact(ratio, rsiPeriod, lowerLimit, upperLimit);
        if (!_trajectories.TryGetValue(selected, out var trajectory))
        {
            trajectory = new Trajectory(selected);
            _trajectories.Add(selected, trajectory);
        }
        double? result = null;
        // An unselected period catches up only when needed, including every
        // intervening price. Restarting at selection would change Wilder seeding.
        while (trajectory.Consumed < count)
            result = trajectory.Calculator.Next(_prices[trajectory.Consumed++]);
        output[0] = result ?? 0;
        output[1] = result.HasValue ? 1 : 0;
    }
}
