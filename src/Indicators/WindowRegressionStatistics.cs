using System.Numerics;
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
        private readonly ExactLinearFitWindow _fit = new(period, observedHistory: true);
        private readonly Queue<double> _history = new();
        private BigInteger _sum,
            _squares,
            _weighted;
        private ExactLinearFitWindow.Fit _latest;
        internal double GlobalIntercept => _latest.OneBasedGlobalIntercept;

        internal double LineAt(int position) => _latest.WindowPosition(position);

        public void Reset()
        {
            _fit.Reset();
            _history.Clear();
            _sum = _squares = _weighted = 0;
            _latest = default;
        }

        public void Dispose()
        {
            _fit.Dispose();
            _history.Clear();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            output[5] = 1;
            var value = ExactVarianceWindow.Units(bar.Close);
            if (_history.Count == period)
            {
                var expired = ExactVarianceWindow.Units(_history.Dequeue());
                _weighted -= _sum - expired;
                _sum -= expired;
                _squares -= expired * expired;
            }
            _weighted += _history.Count * value;
            _sum += value;
            _squares += value * value;
            _history.Enqueue(bar.Close);
            var fit = _fit.Next(bar.Close, true);
            _latest = fit;
            if (_history.Count < 2)
                return;
            var n = new BigInteger(_history.Count);
            var variance = n * _squares - _sum * _sum;
            output[0] = fit.Slope;
            output[1] = fit.OneBasedWindowIntercept;
            output[2] = ExactPopulationDeviation.RootRatio(variance, n * n);
            output[4] = fit.Last;
            output[6] = output[7] = output[9] = 1;
            if (variance.IsZero)
                return;
            var covariance = 2 * _weighted - (n - 1) * _sum;
            output[3] = ExactMeanAccumulator.UnitRatio(
                (3 * covariance * covariance) << 1074,
                (n * n - 1) * variance
            );
            output[8] = 1;
        }
    }
}
