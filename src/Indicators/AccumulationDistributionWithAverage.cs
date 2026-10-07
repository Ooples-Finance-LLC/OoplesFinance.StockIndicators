using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Money-flow multiplier, volume, cumulative accumulation/distribution, and optional simple average.</summary>
/// <remarks>Zero-range candles contribute zero. Each flow is rounded once; the cumulative sum and
/// optional average of published line values are rounded once. Average is zero when absent, with
/// AverageIsDefined zero. Unrepresentable published outputs are rejected by the runtime.</remarks>
public sealed class AccumulationDistributionWithAverage
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates detailed accumulation/distribution with an optional positive SMA period.</summary>
    public AccumulationDistributionWithAverage(int? averagePeriod = null)
        : base(5)
    {
        if (averagePeriod is <= 0)
            throw new ArgumentOutOfRangeException(nameof(averagePeriod));
        AveragePeriod = averagePeriod;
        (Multiplier, Flow, Value, Average, AverageIsDefined) = DeclaredOutputs;
    }

    /// <summary>Optional average window; null disables the average.</summary>
    public int? AveragePeriod { get; }

    /// <summary>(2*close-high-low)/(high-low), or zero for zero range.</summary>
    public IIndicatorOutput Multiplier { get; }

    /// <summary>Money-flow multiplier times volume, rounded once from the complete formula.</summary>
    public IIndicatorOutput Flow { get; }

    /// <summary>Cumulative sum of money-flow volumes, including the first candle.</summary>
    public IIndicatorOutput Value { get; }

    /// <summary>Simple average of the published cumulative values, or a zero placeholder.</summary>
    public IIndicatorOutput Average { get; }

    /// <summary>One for a complete enabled average window; otherwise zero.</summary>
    public IIndicatorOutput AverageIsDefined { get; }

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Value;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(AveragePeriod);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 5)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars, slot),
                    IndicatorErrorBudget.Exact
                )
            );

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars, int slot)
    {
        var values = new double[bars.Count];
        var lines = new double[bars.Count];
        var total = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            var range = R(b.High) - R(b.Low);
            var multiplier =
                range.Sign == 0
                    ? new ReferenceFraction(0)
                    : ((R(b.Close) - R(b.Low)) - (R(b.High) - R(b.Close))) / range;
            var flow = (multiplier * R(b.Volume)).ToDouble();
            total += R(flow);
            lines[i] = total.ToDouble();
            if (slot < 3)
            {
                values[i] = slot switch
                {
                    0 => multiplier.ToDouble(),
                    1 => flow,
                    _ => lines[i],
                };
                continue;
            }
            if (AveragePeriod is not int period || i < period - 1)
                continue;
            var window = lines.Skip(i - period + 1).Take(period).ToArray();
            if (window.Any(double.IsInfinity))
                continue;
            var sum = window.Aggregate(new ReferenceFraction(0), (sum, value) => sum + R(value));
            values[i] = slot == 4 ? 1 : (sum / new ReferenceFraction(period)).ToDouble();
        }
        return values;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    private sealed class State(int? averagePeriod) : IMultiOutputState
    {
        private readonly Queue<double> _window = new();
        private ExactMeanAccumulator _total,
            _sum;

        public void Reset()
        {
            _window.Clear();
            _total = _sum = default;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var numerator = new ExactMeanAccumulator();
            numerator.Add(bar.Close, 2);
            numerator.Add(bar.High, -1);
            numerator.Add(bar.Low, -1);
            var denominator = new ExactMeanAccumulator();
            denominator.Add(bar.High);
            denominator.Add(bar.Low, -1);
            output[0] = numerator.Ratio(denominator);
            var flow = MoneyFlowAccumulationWindow.Flow(bar.High, bar.Low, bar.Close, bar.Volume);
            output[1] = flow.Publish();
            flow.AddTo(ref _total);
            output[2] = _total.Mean(1);
            if (averagePeriod is not int period || double.IsInfinity(output[2]))
                return;
            if (_window.Count == period)
                _sum.Add(_window.Dequeue(), -1);
            _window.Enqueue(output[2]);
            _sum.Add(output[2]);
            if (_window.Count == period)
            {
                output[3] = _sum.Mean(period);
                output[4] = 1;
            }
        }
    }
}
