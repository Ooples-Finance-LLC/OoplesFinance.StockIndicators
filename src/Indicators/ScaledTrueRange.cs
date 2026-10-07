using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>True range divided by a positive integer scale, without temporal smoothing.</summary>
/// <remarks>The first candle uses high-low; subsequent candles also consider gaps from previous
/// close. The complete ratio is rounded once, so an oversized intermediate range may yield a
/// finite scaled value. This is not an average true range.</remarks>
public sealed class ScaledTrueRange : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a true range scaled by the reciprocal of a positive divisor.</summary>
    public ScaledTrueRange(int divisor = 14)
    {
        if (divisor < 1)
            throw new ArgumentOutOfRangeException(nameof(divisor));
        Divisor = divisor;
    }

    /// <summary>Positive divisor applied to each true range independently.</summary>
    public int Divisor { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Divisor);

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
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var high = ReferenceFraction.FromDouble(bars[i].High);
            var low = ReferenceFraction.FromDouble(bars[i].Low);
            var range = high - low;
            if (i > 0)
            {
                var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
                range = new[] { range, (high - previous).Abs(), (low - previous).Abs() }.Max();
            }
            values[i] = (range / new ReferenceFraction(Divisor)).ToDouble();
        }
        return values;
    }

    private sealed class State(int divisor) : IIndicatorState
    {
        private double _previous;
        private bool _started;

        public void Reset()
        {
            _previous = 0;
            _started = false;
        }

        public double Update(in Bar bar)
        {
            var high = ExactVarianceWindow.Units(bar.High);
            var low = ExactVarianceWindow.Units(bar.Low);
            var range = high - low;
            if (_started)
            {
                var previous = ExactVarianceWindow.Units(_previous);
                range = BigInteger.Max(
                    range,
                    BigInteger.Max(BigInteger.Abs(high - previous), BigInteger.Abs(low - previous))
                );
            }
            _started = true;
            _previous = bar.Close;
            return ExactMeanAccumulator.UnitRatio(range, divisor);
        }
    }
}
