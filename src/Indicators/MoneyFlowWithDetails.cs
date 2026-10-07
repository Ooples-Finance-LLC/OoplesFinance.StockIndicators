using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Money-flow multiplier, published flow, and full-window Chaikin money-flow ratio.</summary>
/// <remarks>Multiplier and flow start immediately and are each rounded from the complete
/// formula; zero-range candles publish zero. CMF divides the exact sum of published flows
/// by the exact sum of volumes, without a percentage multiplier or clipping. It is absent
/// before a complete window or when total volume is zero. Histories grow lazily.
/// The runtime rejects unrepresentable published outputs.</remarks>
public sealed class MoneyFlowWithDetails
    : MultiOutputIndicatorBase,
        IVolumeIndicator,
        IIndicatorValidationContract
{
    /// <summary>Creates full-window flow diagnostics for a positive period.</summary>
    public MoneyFlowWithDetails(int period = 20)
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of published flows and volumes in the CMF window.</summary>
    public int Period { get; }

    /// <summary>(2*close-high-low)/(high-low), or zero on an exactly zero range.</summary>
    public IIndicatorOutput Multiplier => Outputs[0];

    /// <summary>Multiplier times volume, rounded once from the complete formula.</summary>
    public IIndicatorOutput Flow => Outputs[1];

    /// <summary>Sum of published flows divided by total volume, or zero when absent.</summary>
    public IIndicatorOutput Cmf => Outputs[2];

    /// <summary>One when a complete window has nonzero exact total volume.</summary>
    public IIndicatorOutput CmfIsDefined => Outputs[3];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Cmf;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 4)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var output = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i];
            var span = R(b.High) - R(b.Low);
            var multiplier =
                span.Sign == 0
                    ? new ReferenceFraction(0)
                    : ((R(b.Close) - R(b.Low)) - (R(b.High) - R(b.Close))) / span;
            output[0][i] = multiplier.ToDouble();
            output[1][i] = (multiplier * R(b.Volume)).ToDouble();
            if (double.IsInfinity(output[0][i]) || double.IsInfinity(output[1][i]))
                return output;
            if (i < Period - 1)
                continue;
            var volume = new ReferenceFraction(0);
            var flow = new ReferenceFraction(0);
            for (var j = i - Period + 1; j <= i; j++)
            {
                volume += R(bars[j].Volume);
                flow += R(output[1][j]);
            }
            if (volume.Sign == 0)
                continue;
            output[2][i] = (flow / volume).ToDouble();
            output[3][i] = 1;
        }
        return output;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<(double Flow, double Volume)> _window = new();
        private ExactMeanAccumulator _flows,
            _volumes;

        public void Reset()
        {
            _window.Clear();
            _flows = _volumes = default;
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
            output[1] = MoneyFlowAccumulationWindow
                .Flow(bar.High, bar.Low, bar.Close, bar.Volume)
                .Publish();
            if (double.IsInfinity(output[0]) || double.IsInfinity(output[1]))
                return;
            if (_window.Count == period)
            {
                var old = _window.Dequeue();
                _flows.Add(old.Flow, -1);
                _volumes.Add(old.Volume, -1);
            }
            _window.Enqueue((output[1], bar.Volume));
            _flows.Add(output[1]);
            _volumes.Add(bar.Volume);
            if (_window.Count < period || _volumes.IsExactlyZero)
                return;
            output[2] = _flows.Ratio(_volumes);
            output[3] = 1;
        }
    }
}
