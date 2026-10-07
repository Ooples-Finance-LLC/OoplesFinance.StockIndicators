using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>SMA-seeded close EMA and the high/low distances from that published EMA.</summary>
/// <remarks>All three values are absent until Period candles have arrived. The seed
/// and complete EMA updates are rounded once, then BullPower=high-EMA and
/// BearPower=low-EMA are rounded independently. Signed powers are not clipped.
/// Storage is constant. The runtime rejects an unrepresentable published result.</remarks>
public sealed class ElderRayWithDetails : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates Elder-ray diagnostics with a positive EMA period.</summary>
    public ElderRayWithDetails(int period = 13)
        : base(4)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>SMA seed length and subsequent EMA period.</summary>
    public int Period { get; }

    /// <summary>Published exponential average of close.</summary>
    public IIndicatorOutput Ema => Outputs[0];

    /// <summary>High minus the published EMA.</summary>
    public IIndicatorOutput BullPower => Outputs[1];

    /// <summary>Low minus the published EMA.</summary>
    public IIndicatorOutput BearPower => Outputs[2];

    /// <summary>One when all three values are present; otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[3];

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
        var result = Enumerable.Range(0, 4).Select(_ => new double[bars.Count]).ToArray();
        if (bars.Count < Period)
            return result;
        var seed = new ReferenceFraction(0);
        for (var i = 0; i < Period; i++)
            seed += ReferenceFraction.FromDouble(bars[i].Close);
        var ema = (seed / new ReferenceFraction(Period)).ToDouble();
        for (var i = Period - 1; i < bars.Count; i++)
        {
            if (i >= Period)
                ema = (
                    (
                        new ReferenceFraction(2) * ReferenceFraction.FromDouble(bars[i].Close)
                        + new ReferenceFraction(Period - 1) * ReferenceFraction.FromDouble(ema)
                    ) / new ReferenceFraction((long)Period + 1)
                ).ToDouble();
            result[0][i] = ema;
            result[1][i] = (
                ReferenceFraction.FromDouble(bars[i].High) - ReferenceFraction.FromDouble(ema)
            ).ToDouble();
            result[2][i] = (
                ReferenceFraction.FromDouble(bars[i].Low) - ReferenceFraction.FromDouble(ema)
            ).ToDouble();
            result[3][i] = 1;
        }
        return result;
    }

    private sealed class State(int period) : IMultiOutputState
    {
        private ExactMeanAccumulator _seed;
        private int _count;
        private double _ema;

        public void Reset()
        {
            _seed = default;
            _count = 0;
            _ema = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (_count < period)
            {
                _seed.Add(bar.Close);
                if (++_count < period)
                    return;
                _ema = _seed.Mean(period);
            }
            else
            {
                var update = new ExactMeanAccumulator();
                update.Add(bar.Close, 2);
                update.Add(_ema, period - 1);
                _ema = update.Mean((long)period + 1);
            }
            var bull = new ExactMeanAccumulator();
            bull.Add(bar.High);
            bull.Add(_ema, -1);
            var bear = new ExactMeanAccumulator();
            bear.Add(bar.Low);
            bear.Add(_ema, -1);
            output[0] = _ema;
            output[1] = bull.Mean(1);
            output[2] = bear.Mean(1);
            output[3] = 1;
        }
    }
}
