using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Early-start Hilbert trendline, smoothed price, and rounded dominant cycle length.</summary>
/// <remarks>Publishes the input through index 10, then a 4/3/2/1 weighted cycle mean.
/// Filtering and smoothed prices begin at index 6. Undefined cycle lengths have a
/// separate presence output. Logical stages round once with wide intermediates;
/// all history is bounded. The default input is the high/low midpoint.</remarks>
public sealed class SeededHilbertTrendline : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a trendline using the high/low midpoint, or close when false.</summary>
    public SeededHilbertTrendline(bool useMidpoint = true)
        : base(6) => UseMidpoint = useMidpoint;

    /// <summary>Whether to use the high/low midpoint instead of close.</summary>
    public bool UseMidpoint { get; }

    /// <summary>Trendline, including raw input during startup.</summary>
    public IIndicatorOutput Trendline => Outputs[0];

    /// <summary>Four-price weighted smoothing.</summary>
    public IIndicatorOutput SmoothPrice => Outputs[1];

    /// <summary>Rounded dominant cycle length.</summary>
    public IIndicatorOutput DcPeriods => Outputs[2];

    /// <summary>Trendline presence.</summary>
    public IIndicatorOutput IsTrendlineDefined => Outputs[3];

    /// <summary>Smoothed price presence.</summary>
    public IIndicatorOutput IsSmoothPriceDefined => Outputs[4];

    /// <summary>Cycle length presence.</summary>
    public IIndicatorOutput IsDcPeriodsDefined => Outputs[5];

    /// <inheritdoc/>
    public override int WarmupBars => 11;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(UseMidpoint);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        SeededHilbertTrendReference
                            .Values(bars, UseMidpoint)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(bool midpoint) : IMultiOutputState
    {
        private readonly HilbertPhaseState _filter = new(6, false, false);
        private readonly HilbertTrendMeanState _means = new();
        private long _index = -1;
        private double _period;

        public void Reset()
        {
            _filter.Reset();
            _means.Reset();
            _index = -1;
            _period = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var price = midpoint
                ? RocBankValue.RoundUnits(
                    ExactVarianceWindow.Units(bar.High) + ExactVarianceWindow.Units(bar.Low),
                    2
                )
                : ExactVarianceWindow.Units(bar.Close);
            _filter.Update(price);
            output[0] = ExactMeanAccumulator.UnitRatio(price, 1);
            output[3] = 1;
            if (_index < 6)
            {
                _means.Update(price, null);
                return;
            }
            _period = .33 * _filter.Period + .67 * _period;
            var count = (int)Math.Min((int)(_period + .5), _index + 1);
            _means.Update(price, count == 0 ? 1 : count);
            if (_index >= 11)
                output[0] = ExactMeanAccumulator.UnitRatio(_means.RoundedUnits, 1);
            output[1] = ExactMeanAccumulator.UnitRatio(_filter.SmoothPrice, 1);
            output[4] = 1;
            if (count > 0)
            {
                output[2] = count;
                output[5] = 1;
            }
        }
    }
}
