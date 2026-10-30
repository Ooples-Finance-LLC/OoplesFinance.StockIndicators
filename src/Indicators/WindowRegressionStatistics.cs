using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Rolling least-squares slope, intercept, population deviation, R-squared and fitted endpoint.</summary>
/// <remarks>Coordinates are 1..count within the current window. Available history is used
/// during startup. Slope is zero for one observation; the other statistics are absent.
/// R-squared is absent for a constant window, including after a varying window. Missing
/// values have zero placeholders and explicit presence outputs. Each exact moment-derived
/// statistic is rounded independently. History grows only as observations arrive.</remarks>
public sealed class WindowRegressionStatistics
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates rolling regression statistics for a window of at least two observations.</summary>
    public WindowRegressionStatistics(int period = 14)
        : base(10)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Maximum number of observations in the fit.</summary>
    public int Period { get; }

    /// <summary>Fitted price change per candle.</summary>
    public IIndicatorOutput Slope => Outputs[0];

    /// <summary>Fitted price at window-local coordinate zero, one candle before its first observation.</summary>
    public IIndicatorOutput Intercept => Outputs[1];

    /// <summary>Population standard deviation of closes.</summary>
    public IIndicatorOutput StandardDeviation => Outputs[2];

    /// <summary>Squared correlation of window-local time and close.</summary>
    public IIndicatorOutput RSquared => Outputs[3];

    /// <summary>Fitted price at the latest candle.</summary>
    public IIndicatorOutput Line => Outputs[4];

    /// <summary>Always one for an observed candle.</summary>
    public IIndicatorOutput SlopeIsDefined => Outputs[5];

    /// <summary>One when at least two observations are available.</summary>
    public IIndicatorOutput InterceptIsDefined => Outputs[6];

    /// <summary>One when at least two observations are available.</summary>
    public IIndicatorOutput StandardDeviationIsDefined => Outputs[7];

    /// <summary>One when at least two observations have nonzero variance.</summary>
    public IIndicatorOutput RSquaredIsDefined => Outputs[8];

    /// <summary>One when at least two observations are available.</summary>
    public IIndicatorOutput LineIsDefined => Outputs[9];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Slope;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 10)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => Reference(bars, Period)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private static double[][] Reference(IReadOnlyList<Bar> bars, int period)
    {
        var output = Enumerable.Range(0, 10).Select(_ => new double[bars.Count]).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            output[5][i] = 1;
            var count = Math.Min(period, i + 1);
            if (count < 2)
                continue;
            var values = bars.Skip(i - count + 1)
                .Take(count)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var n = new ReferenceFraction(count);
            var mean = values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n;
            var center = new ReferenceFraction(count + 1L) / new ReferenceFraction(2);
            var xx = new ReferenceFraction(0);
            var yy = new ReferenceFraction(0);
            var xy = new ReferenceFraction(0);
            for (var j = 0; j < count; j++)
            {
                var x = new ReferenceFraction(j + 1L) - center;
                var y = values[j] - mean;
                xx += x * x;
                yy += y * y;
                xy += x * y;
            }
            var slope = xy / xx;
            output[0][i] = slope.ToDouble();
            output[1][i] = (mean - slope * center).ToDouble();
            output[2][i] = (yy / n).SqrtToDouble();
            output[4][i] = (mean + slope * (n - center)).ToDouble();
            output[6][i] = output[7][i] = output[9][i] = 1;
            if (!yy.Components.Numerator.IsZero)
            {
                output[3][i] = (xy * xy / (xx * yy)).ToDouble();
                output[8][i] = 1;
            }
        }
        return output;
    }

    internal sealed class State(int period) : IMultiOutputState, IDisposable
    {
        private readonly Queue<double> _history = new();
        private JurikDyadic _sum, _squares, _weighted, _seen;
        private JurikDyadic _base, _threeCovariance, _denominator;

        internal double GlobalIntercept
        {
            get
            {
                var position = _seen;
                position.Multiply(-2);
                position.Add(_history.Count - 1d);
                return At(position);
            }
        }

        internal double LineAt(int position) => At(JurikDyadic.FromDouble(2L * position - (_history.Count - 1L)));

        // Bounded replay retains local moments but global-coordinate consumers
        // must supply the original one-based observation count.
        internal double GlobalInterceptAt(int seen)
        {
            var position = JurikDyadic.FromDouble(seen);
            position.Multiply(-2);
            position.Add(_history.Count - 1d);
            return At(position);
        }

        private double At(JurikDyadic twiceCenteredPosition)
        {
            if (_history.Count < 2) return _sum.Mean(1);
            var numerator = _threeCovariance;
            numerator.Multiply(twiceCenteredPosition);
            numerator.AddExact(_base);
            return numerator.Ratio(_denominator);
        }

        public void Reset()
        {
            _history.Clear();
            _sum = _squares = _weighted = _seen = default;
            _base = _threeCovariance = _denominator = default;
        }

        public void Dispose() => _history.Clear();

        // Historical block consumers need moments on every bar, but fitted
        // outputs only at the block endpoint. Call Update before reading a fit.
        internal void Accumulate(in Bar bar)
        {
            var current = JurikDyadic.FromDouble(bar.Close);
            if (_history.Count == period)
            {
                var expired = JurikDyadic.FromDouble(_history.Dequeue());
                _sum.Subtract(expired);
                _weighted.Subtract(_sum);
                expired.Square();
                _squares.Subtract(expired);
            }
            var weighted = current;
            weighted.Multiply(_history.Count);
            _weighted.AddExact(weighted);
            _sum.AddExact(current);
            current.Square();
            _squares.AddExact(current);
            _history.Enqueue(bar.Close);
            _seen.Add(1);
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            output[5] = 1;
            Accumulate(bar);
            int count = _history.Count;
            if (count < 2) return;

            // Centered moments remain exact. Ordinary prices fit the existing
            // fixed-width dyadic engine; wide exponent spans retain its fallback.
            var variance = _squares;
            variance.Multiply(count);
            var sumSquared = _sum;
            sumSquared.Square();
            variance.Subtract(sumSquared);
            var covariance = _weighted;
            covariance.Multiply(2);
            var centeredSum = _sum;
            centeredSum.Multiply(count - 1);
            covariance.Subtract(centeredSum);
            var spread = JurikDyadic.FromDouble(count);
            spread.Multiply(count);
            spread.Add(-1);
            _denominator = spread;
            _denominator.Multiply(count);
            _base = _sum;
            _base.Multiply(spread);
            _threeCovariance = covariance;
            _threeCovariance.Multiply(3);

            var slope = _threeCovariance;
            slope.Multiply(2);
            output[0] = slope.Ratio(_denominator);
            output[1] = At(JurikDyadic.FromDouble(-1L - count));
            output[2] = variance.SqrtMean((long)count * count);
            output[4] = At(JurikDyadic.FromDouble(count - 1));
            output[6] = output[7] = output[9] = 1;
            if (variance.Sign == 0) return;
            covariance.Square();
            covariance.Multiply(3);
            variance.Multiply(spread);
            output[3] = covariance.Ratio(variance);
            output[8] = 1;
        }
    }
}
