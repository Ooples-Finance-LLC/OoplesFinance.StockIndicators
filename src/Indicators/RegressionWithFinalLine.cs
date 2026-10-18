using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Full-window regression statistics with the final window's retrospective fitted line.</summary>
/// <remarks>Only finite sources are supported. Appending bars repaints the Line output:
/// only the last Period rows contain that final fit. Missing values are zero with explicit
/// presence outputs. Intercepts use global one-based coordinates, including source warmup.</remarks>
public sealed class RegressionWithFinalLine : MultiOutputIndicatorBase, IHistoricalIndicator, IIndicatorValidationContract
{
    /// <summary>Creates a regression with a full window of at least two bars.</summary>
    public RegressionWithFinalLine(int period = 14) : base(10)
    {
        if (period < 2) throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }
    /// <summary>Number of observations required for a complete fit.</summary>
    public int Period { get; }
    /// <summary>Rolling fitted slope.</summary>
    public IIndicatorOutput Slope => Outputs[0];
    /// <summary>Rolling intercept at global coordinate zero.</summary>
    public IIndicatorOutput Intercept => Outputs[1];
    /// <summary>Rolling population standard deviation.</summary>
    public IIndicatorOutput StandardDeviation => Outputs[2];
    /// <summary>Rolling squared correlation, absent for flat windows.</summary>
    public IIndicatorOutput RSquared => Outputs[3];
    /// <summary>Final window's fitted line, absent outside that window.</summary>
    public IIndicatorOutput Line => Outputs[4];
    /// <summary>One when a full rolling window is available.</summary>
    public IIndicatorOutput SlopeIsDefined => Outputs[5];
    /// <summary>One when a full rolling window is available.</summary>
    public IIndicatorOutput InterceptIsDefined => Outputs[6];
    /// <summary>One when a full rolling window is available.</summary>
    public IIndicatorOutput StandardDeviationIsDefined => Outputs[7];
    /// <summary>One when a full nonconstant window is available.</summary>
    public IIndicatorOutput RSquaredIsDefined => Outputs[8];
    /// <summary>One inside the final complete window.</summary>
    public IIndicatorOutput LineIsDefined => Outputs[9];
    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Slope;
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 10).Select(slot =>
        IndicatorValidationRule.ReferenceWithOverflowRejection(slot,
            bars => Reference(bars, Period)[slot], IndicatorErrorBudget.Exact));

    double[][] IHistoricalIndicator.CalculateHistory(IReadOnlyList<Bar> bars)
    {
        var values = Enumerable.Range(0, 10).Select(slot => slot is 6 or 7 ? Array.Empty<double>() : new double[bars.Count]).ToArray();
        values[6] = values[7] = values[5];
        using var writer = new Writer(Period, bars.Count);
        Span<double> scratch = stackalloc double[10];
        for (int i = 0; i < bars.Count; i++) writer.Append(bars[i], i, values, scratch);
        writer.Complete(values, default);
        return values;
    }

    // No state is created for live execution. Both finite paths share a column writer,
    // and only completion writes the retrospective overlay.
    internal sealed class Writer(int period, int count) : IDisposable
    {
        private readonly WindowRegressionStatistics.State? _window = count < period ? null : new(period);
        internal void Append(in Bar bar, int index, double[][] values, Span<double> scratch)
        {
            if (_window is null) return;
            _window.Update(bar, scratch);
            if (index < period - 1) return;
            values[0][index] = scratch[0];
            values[1][index] = _window.GlobalIntercept;
            values[2][index] = scratch[2];
            values[3][index] = scratch[3];
            values[5][index] = values[6][index] = values[7][index] = 1;
            values[8][index] = scratch[8];
        }
        internal void Complete(double[][] values, CancellationToken cancellation)
        {
            if (_window is null) return;
            for (int position = 0; position < period; position++)
            {
                cancellation.ThrowIfCancellationRequested();
                int index = count - period + position;
                values[4][index] = _window.LineAt(position);
                values[9][index] = 1;
            }
        }
        public void Dispose() => _window?.Dispose();
    }

    // Independent centered rational fits; no rolling production state or rounded
    // slope/intercept is reused when calculating global intercepts and line points.
    private static double[][] Reference(IReadOnlyList<Bar> bars, int period)
    {
        var result = Enumerable.Range(0, 10).Select(_ => new double[bars.Count]).ToArray();
        var n = new ReferenceFraction(period);
        var center = new ReferenceFraction(period + 1L) / new ReferenceFraction(2);
        for (int end = period - 1; end < bars.Count; end++)
        {
            var mean = new ReferenceFraction(0);
            for (int j = 0; j < period; j++) mean += ReferenceFraction.FromDouble(bars[end - period + 1 + j].Close);
            mean /= n;
            var xx = new ReferenceFraction(0);
            var yy = new ReferenceFraction(0);
            var xy = new ReferenceFraction(0);
            for (int j = 0; j < period; j++)
            {
                var x = new ReferenceFraction(j + 1L) - center;
                var y = ReferenceFraction.FromDouble(bars[end - period + 1 + j].Close) - mean;
                xx += x * x; yy += y * y; xy += x * y;
            }
            var slope = xy / xx;
            result[0][end] = slope.ToDouble();
            result[1][end] = (mean - slope * (center + new ReferenceFraction(end - period + 1L))).ToDouble();
            result[2][end] = (yy / n).SqrtToDouble();
            result[5][end] = result[6][end] = result[7][end] = 1;
            if (!yy.Components.Numerator.IsZero)
            {
                result[3][end] = (xy * xy / (xx * yy)).ToDouble();
                result[8][end] = 1;
            }
            if (end != bars.Count - 1) continue;
            for (int j = 0; j < period; j++)
            {
                result[4][end - period + 1 + j] = (mean + slope * (new ReferenceFraction(j + 1L) - center)).ToDouble();
                result[9][end - period + 1 + j] = 1;
            }
        }
        return result;
    }
}
