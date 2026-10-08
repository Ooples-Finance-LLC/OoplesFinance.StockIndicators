using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Dominant-cycle mean followed by a four-bar weighted trendline.</summary>
/// <remarks>Zero-seeded Hilbert filters start at index 37; publication starts
/// at 63+suppression. The smoothed cycle period rounds by truncating period+0.5.
/// Each selected close-price mean rounds once, then the 4/3/2/1 trendline rounds
/// once. Zero cycle length yields zero. Wide filter stages preserve finite
/// controls and averages at extreme prices. Storage is bounded to fifty prices.</remarks>
public sealed class DelayedHilbertTrendline : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a nonnegative additional publication suppression.</summary>
    public DelayedHilbertTrendline(int suppression = 0)
        : base(2) => Suppression = HilbertCycleState.Validate(suppression);

    /// <summary>Additional bars withheld before publication.</summary>
    public int Suppression { get; }

    /// <summary>Weighted trendline, or zero before publication.</summary>
    public IIndicatorOutput Trendline => Outputs[0];

    /// <summary>Trendline presence.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    public override int WarmupBars => (int)Math.Min(int.MaxValue, 63L + Suppression);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Suppression);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        DelayedHilbertTrendReference
                            .Values(bars, Suppression)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int suppression) : IMultiOutputState
    {
        private readonly HilbertPhaseState _phase = new(37, true, false);
        private readonly HilbertTrendMeanState _trend = new();
        private long _index = -1;
        private double _period;

        public void Reset()
        {
            _phase.Reset();
            _trend.Reset();
            _index = -1;
            _period = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _index++;
            var price = ExactVarianceWindow.Units(bar.Close);
            _phase.Update(price);
            if (_index >= 37)
            {
                _period = .33 * _phase.Period + .67 * _period;
                var count = (int)(_period + .5);
                _trend.Update(price, count);
                if (_index >= 63L + suppression)
                {
                    output[0] = ExactMeanAccumulator.UnitRatio(_trend.RoundedUnits, 1);
                    output[1] = 1;
                }
            }
            else
                _trend.Update(price, null);
        }
    }
}
