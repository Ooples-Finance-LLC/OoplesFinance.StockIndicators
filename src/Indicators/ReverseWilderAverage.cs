using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>An expanding rounded mean followed by a reverse Wilder recurrence.</summary>
/// <remarks>This explicitly reproduces the direction of QuanTAlib 1.0.0 Rma:
/// previous + (previous-close)/period after Period inputs. It is an extrapolator,
/// not conventional Wilder smoothing, and may diverge on bounded prices. Use
/// WilderMovingAverage for smoothing. Startup uses the previous rounded mean;
/// each complete update is rounded once. Storage is constant and the runtime
/// rejects an unrepresentable final output.</remarks>
public sealed class ReverseWilderAverage : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a reverse recurrence with a positive period.</summary>
    public ReverseWilderAverage(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Expanding-mean seed length and subsequent extrapolation divisor.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (i == 0)
                result[i] = bars[i].Close;
            else
            {
                var previous = ReferenceFraction.FromDouble(result[i - 1]);
                var current = ReferenceFraction.FromDouble(bars[i].Close);
                var next =
                    i < Period
                        ? (previous * new ReferenceFraction(i) + current)
                            / new ReferenceFraction(i + 1)
                        : previous + (previous - current) / new ReferenceFraction(Period);
                result[i] = next.ToDouble();
                if (double.IsInfinity(result[i]))
                    break;
            }
        }
        return result;
    }

    private sealed class State(int period) : IIndicatorState
    {
        private int _seedCount;
        private double _previous;

        public void Reset()
        {
            _seedCount = 0;
            _previous = 0;
        }

        public double Update(in Bar bar)
        {
            if (_seedCount == 0)
            {
                _seedCount = 1;
                _previous = bar.Close;
                return _previous;
            }
            var sum = new ExactMeanAccumulator();
            if (_seedCount < period)
            {
                sum.Add(_previous, _seedCount);
                sum.Add(bar.Close);
                _previous = sum.Mean(++_seedCount);
            }
            else
            {
                // Split period+1 to avoid overflowing its integer representation.
                sum.Add(_previous, period);
                sum.Add(_previous);
                sum.Add(bar.Close, -1);
                _previous = sum.Mean(period);
            }
            return _previous;
        }
    }
}
