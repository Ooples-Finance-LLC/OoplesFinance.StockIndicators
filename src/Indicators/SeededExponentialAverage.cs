using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Initialization for the stages of a double or triple exponential average.</summary>
public enum ExponentialSeedMode
{
    /// <summary>Initialize every stage with the same first price SMA.</summary>
    Shared,

    /// <summary>Initialize each stage with its own SMA of the preceding stage's mature values.</summary>
    Cascaded,
}

/// <summary>Double or triple EMA extrapolation with an explicit seeding formula.</summary>
/// <remarks>Order two returns 2*EMA1-EMA2; order three returns 3*EMA1-3*EMA2+EMA3.
/// Each seed, recurrence and final combination rounds once. Startup returns zero.
/// True output overflow is rejected by the runtime. State uses constant space even for large periods.</remarks>
public sealed class SeededExponentialAverage
    : IndicatorBase,
        IMovingAverage,
        ITrendIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a double or triple exponential average with positive period and explicit initialization.</summary>
    public SeededExponentialAverage(
        int period = 14,
        int order = 2,
        ExponentialSeedMode seedMode = ExponentialSeedMode.Shared
    )
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (order is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(order));
        if (!Enum.IsDefined(typeof(ExponentialSeedMode), seedMode))
            throw new ArgumentOutOfRangeException(nameof(seedMode));
        Period = period;
        Order = order;
        SeedMode = seedMode;
    }

    /// <summary>SMA seed and EMA smoothing period.</summary>
    public int Period { get; }

    /// <summary>Two for DEMA, three for TEMA.</summary>
    public int Order { get; }

    /// <summary>Shared initial SMA or separately seeded stages.</summary>
    public ExponentialSeedMode SeedMode { get; }

    /// <inheritdoc/>
    /// <remarks>Metadata saturates at int.MaxValue when the cascaded startup exceeds the integer range.</remarks>
    public override int WarmupBars =>
        (int)
            Math.Min(
                int.MaxValue,
                (long)(Period - 1) * (SeedMode == ExponentialSeedMode.Shared ? 1 : Order)
            );

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Order, SeedMode);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    internal IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var layers = new double[Order][];
        for (var stage = 0; stage < Order; stage++)
        {
            layers[stage] = new double[bars.Count];
            var first =
                (long)(Period - 1) * (SeedMode == ExponentialSeedMode.Shared ? 1 : stage + 1);
            if (first >= bars.Count)
                continue;
            var seed = new ReferenceFraction(0);
            if (SeedMode == ExponentialSeedMode.Shared || stage == 0)
                for (var j = 0; j < Period; j++)
                    seed += ReferenceFraction.FromDouble(bars[j].Close);
            else
                for (var j = (int)first - Period + 1; j <= first; j++)
                    seed += ReferenceFraction.FromDouble(layers[stage - 1][j]);
            layers[stage][(int)first] = (seed / new ReferenceFraction(Period)).ToDouble();
            for (var i = (int)first + 1; i < bars.Count; i++)
            {
                var input = stage == 0 ? bars[i].Close : layers[stage - 1][i];
                layers[stage][i] = (
                    (
                        ReferenceFraction.FromDouble(input) * new ReferenceFraction(2)
                        + ReferenceFraction.FromDouble(layers[stage][i - 1])
                            * new ReferenceFraction(Period - 1)
                    ) / new ReferenceFraction((long)Period + 1)
                ).ToDouble();
            }
        }
        var result = new double[bars.Count];
        for (var i = WarmupBars; i < bars.Count; i++)
        {
            var first = ReferenceFraction.FromDouble(layers[0][i]);
            var second = ReferenceFraction.FromDouble(layers[1][i]);
            result[i] = (
                Order == 2
                    ? first * new ReferenceFraction(2) - second
                    : (first - second) * new ReferenceFraction(3)
                        + ReferenceFraction.FromDouble(layers[2][i])
            ).ToDouble();
        }
        return result;
    }

    private sealed class State(int period, int order, ExponentialSeedMode mode) : IIndicatorState
    {
        private readonly ExactMeanAccumulator[] _seeds = new ExactMeanAccumulator[order];
        private readonly int[] _counts = new int[order];
        private readonly double[] _averages = new double[order];

        public void Reset()
        {
            Array.Clear(_seeds, 0, order);
            Array.Clear(_counts, 0, order);
            Array.Clear(_averages, 0, order);
        }

        public double Update(in Bar bar)
        {
            if (mode == ExponentialSeedMode.Shared && _counts[0] < period)
            {
                _seeds[0].Add(bar.Close);
                if (++_counts[0] < period)
                    return 0;
                var seed = _seeds[0].Mean(period);
                for (var i = 0; i < order; i++)
                {
                    _averages[i] = seed;
                    _counts[i] = period;
                }
                return seed;
            }
            var value = bar.Close;
            for (var i = 0; i < order; i++)
            {
                if (_counts[i] < period)
                {
                    _seeds[i].Add(value);
                    if (++_counts[i] < period)
                        return 0;
                    _averages[i] = _seeds[i].Mean(period);
                }
                else
                {
                    var sum = new ExactMeanAccumulator();
                    sum.Add(value, 2);
                    sum.Add(_averages[i], period - 1);
                    _averages[i] = sum.Mean((long)period + 1);
                }
                value = _averages[i];
            }
            var combination = new ExactMeanAccumulator();
            combination.Add(_averages[0], order);
            combination.Add(_averages[1], order == 2 ? -1 : -3);
            if (order == 3)
                combination.Add(_averages[2]);
            return combination.Mean(1);
        }
    }
}
