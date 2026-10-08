using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Volume-weighted price change with an SMA seed followed by an EMA.</summary>
/// <remarks>The first candle supplies the previous close. The next Period changes seed
/// the first output; subsequent readings use alpha 2/(Period+1). Products, seed sums, and
/// each recurrence are evaluated exactly before rounding the published reading. Unpublished
/// raw changes may exceed binary64 range. Storage is constant, including maximum periods.</remarks>
public sealed class SeededForceIndex
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates a force index with a positive seeding and smoothing period.</summary>
    public SeededForceIndex(int period = 13)
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of price changes in the seed and smoothing denominator.</summary>
    public int Period { get; }

    /// <summary>Force index, or zero before the seed is complete.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One after a complete seed of Period price changes.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Value;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = new[] { new double[bars.Count], new double[bars.Count] };
        var seed = new ReferenceFraction(0);
        var previous = new ReferenceFraction(0);
        for (var i = 1; i < bars.Count; i++)
        {
            var raw =
                (
                    ReferenceFraction.FromDouble(bars[i].Close)
                    - ReferenceFraction.FromDouble(bars[i - 1].Close)
                ) * ReferenceFraction.FromDouble(bars[i].Volume);
            if (i <= Period)
                seed += raw;
            if (i < Period)
                continue;
            var value =
                i == Period
                    ? seed / new ReferenceFraction(Period)
                    : (
                        previous * new ReferenceFraction((long)Period - 1)
                        + raw * new ReferenceFraction(2)
                    ) / new ReferenceFraction((long)Period + 1);
            result[0][i] = value.ToDouble();
            result[1][i] = 1;
            if (double.IsInfinity(result[0][i]))
                return result;
            previous = ReferenceFraction.FromDouble(result[0][i]);
        }
        return result;
    }

    private sealed class State(int period) : IMultiOutputState
    {
        private ExactMeanAccumulator _seed;
        private double _previousPrice,
            _value;
        private bool _started;
        private int _changes;

        public void Reset()
        {
            _seed = default;
            _previousPrice = _value = 0;
            _started = false;
            _changes = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (!_started)
            {
                _previousPrice = bar.Close;
                _started = true;
                return;
            }
            if (_changes < period)
            {
                _seed.AddProduct(bar.Close, bar.Volume);
                _seed.AddProduct(_previousPrice, bar.Volume, -1);
                _changes++;
                if (_changes == period)
                {
                    _value = _seed.Mean(period);
                    output[0] = _value;
                    output[1] = 1;
                }
            }
            else
            {
                var next = new ExactMeanAccumulator();
                next.Add(_value, period - 1);
                next.AddProduct(bar.Close, bar.Volume, 2);
                next.AddProduct(_previousPrice, bar.Volume, -2);
                _value = next.Mean((long)period + 1);
                output[0] = _value;
                output[1] = 1;
            }
            _previousPrice = bar.Close;
        }
    }
}
