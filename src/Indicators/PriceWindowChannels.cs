using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Highest high minus lowest low in the available current rolling window.</summary>
/// <remarks>The difference is rounded once. History grows lazily; the runtime rejects an unrepresentable final range.</remarks>
public sealed class WindowPriceRange : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a high-low range for a positive period.</summary>
    public WindowPriceRange(int period = 20)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Maximum number of candles in the window.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => PriceWindowChannelReference.Range(bars, Period),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(int period) : IIndicatorState
    {
        private readonly WindowExtremeDeque _high = new(period, true),
            _low = new(period, false);

        public void Reset()
        {
            _high.Reset();
            _low.Reset();
        }

        public double Update(in Bar bar)
        {
            _high.Add(bar.High);
            _low.Add(bar.Low);
            var difference = new ExactMeanAccumulator();
            difference.Add(_high.Value);
            difference.Add(_low.Value, -1);
            return difference.Mean(1);
        }
    }
}

/// <summary>High, low, midpoint, and relative width of the complete preceding candle window.</summary>
/// <remarks>The current candle is excluded. Each output has a presence flag; width is absent
/// when the exact midpoint is zero. Width is (high-low)/((high+low)/2), evaluated before
/// rounding the midpoint. Negative prices retain their true extrema and signed width.
/// Histories grow lazily. The runtime rejects unrepresentable published outputs.</remarks>
public sealed class PriorPriceChannel : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a channel requiring a positive number of preceding candles.</summary>
    public PriorPriceChannel(int period = 20)
        : base(8)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of preceding candles used for each reading.</summary>
    public int Period { get; }

    /// <summary>Maximum high in the preceding window.</summary>
    public IIndicatorOutput Upper => Outputs[0];

    /// <summary>Minimum low in the preceding window.</summary>
    public IIndicatorOutput Lower => Outputs[1];

    /// <summary>Once-rounded midpoint of the extrema.</summary>
    public IIndicatorOutput Center => Outputs[2];

    /// <summary>Range divided by the exact midpoint, with no percentage multiplier.</summary>
    public IIndicatorOutput Width => Outputs[3];

    /// <summary>One when a complete preceding window exists.</summary>
    public IIndicatorOutput UpperIsDefined => Outputs[4];

    /// <summary>One when a complete preceding window exists.</summary>
    public IIndicatorOutput LowerIsDefined => Outputs[5];

    /// <summary>One when a complete preceding window exists.</summary>
    public IIndicatorOutput CenterIsDefined => Outputs[6];

    /// <summary>One when a complete preceding window has a nonzero exact midpoint.</summary>
    public IIndicatorOutput WidthIsDefined => Outputs[7];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Center;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 8)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => PriceWindowChannelReference.Prior(bars, Period)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly WindowExtremeDeque _high = new(period, true),
            _low = new(period, false);
        private int _count;

        public void Reset()
        {
            _high.Reset();
            _low.Reset();
            _count = 0;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            if (_count == period)
            {
                outputs[0] = _high.Value;
                outputs[1] = _low.Value;
                var sum = new ExactMeanAccumulator();
                sum.Add(_high.Value);
                sum.Add(_low.Value);
                outputs[2] = sum.Mean(2);
                outputs[4] = outputs[5] = outputs[6] = 1;
                if (!sum.IsExactlyZero)
                {
                    var twiceRange = new ExactMeanAccumulator();
                    twiceRange.Add(_high.Value, 2);
                    twiceRange.Add(_low.Value, -2);
                    outputs[3] = twiceRange.Ratio(sum);
                    outputs[7] = 1;
                }
            }
            _high.Add(bar.High);
            _low.Add(bar.Low);
            if (_count < period)
                _count++;
        }
    }
}

internal static class PriceWindowChannelReference
{
    internal static double[] Range(IReadOnlyList<Bar> bars, int period)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - period + 1))
                .Take(Math.Min(period, i + 1))
                .ToArray();
            result[i] = (
                ReferenceFraction.FromDouble(window.Max(b => b.High))
                - ReferenceFraction.FromDouble(window.Min(b => b.Low))
            ).ToDouble();
        }
        return result;
    }

    internal static double[][] Prior(IReadOnlyList<Bar> bars, int period)
    {
        var result = Enumerable.Range(0, 8).Select(_ => new double[bars.Count]).ToArray();
        for (var i = period; i < bars.Count; i++)
        {
            var window = bars.Skip(i - period).Take(period).ToArray();
            var upper = window.Max(b => b.High);
            var lower = window.Min(b => b.Low);
            var high = ReferenceFraction.FromDouble(upper);
            var low = ReferenceFraction.FromDouble(lower);
            var center = (high + low) / new ReferenceFraction(2);
            result[0][i] = upper;
            result[1][i] = lower;
            result[2][i] = center.ToDouble();
            result[4][i] = result[5][i] = result[6][i] = 1;
            if (center.Sign == 0)
                continue;
            result[3][i] = ((high - low) / center).ToDouble();
            result[7][i] = 1;
        }
        return result;
    }
}
