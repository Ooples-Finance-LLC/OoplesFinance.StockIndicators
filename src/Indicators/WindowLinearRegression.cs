using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>A reading from the least-squares line fitted to a rolling window of closes.</summary>
public enum WindowRegressionOutput
{
    /// <summary>Fitted value at the latest candle.</summary>
    Endpoint,

    /// <summary>Extrapolated value one candle after the latest candle.</summary>
    Forecast,

    /// <summary>Price change per candle along the fitted line.</summary>
    Slope,

    /// <summary>Intercept at the oldest candle, whose window-local coordinate is zero.</summary>
    Intercept,

    /// <summary>Arctangent of the fitted slope, in degrees.</summary>
    Angle,
}

/// <summary>Selected scalar output of an exact rolling least-squares line.</summary>
/// <remarks>Window-local x coordinates are 0..count-1. Startup uses available history.
/// A single observation has zero slope and angle; all price readings equal its close.
/// Only the selected reading is published, independently rounded; an oversized unselected
/// slope or forecast does not invalidate a finite endpoint. History grows lazily.</remarks>
public sealed class WindowLinearRegression : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period window regression and selects its published reading.</summary>
    public WindowLinearRegression(
        int period = 14,
        WindowRegressionOutput output = WindowRegressionOutput.Endpoint
    )
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(output))
            throw new ArgumentOutOfRangeException(nameof(output));
        Period = period;
        Output = output;
    }

    /// <summary>Maximum observations in the rolling fit.</summary>
    public int Period { get; }

    /// <summary>Selected endpoint, forecast, slope, window-local intercept, or slope angle in degrees.</summary>
    public WindowRegressionOutput Output { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Output);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                Reference,
                IndicatorErrorBudget.Exact
            ),
        ];

    internal IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(Period, i + 1);
            var window = bars.Skip(i - count + 1)
                .Take(count)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var mean =
                window.Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                / new ReferenceFraction(count);
            var center = new ReferenceFraction(count - 1) / new ReferenceFraction(2);
            var xy = new ReferenceFraction(0);
            var xx = new ReferenceFraction(0);
            for (var j = 0; j < count; j++)
            {
                var x = new ReferenceFraction(j) - center;
                xx += x * x;
                xy += x * (window[j] - mean);
            }
            var slope = count == 1 ? new ReferenceFraction(0) : xy / xx;
            if (Output == WindowRegressionOutput.Angle)
            {
                result[i] = Math.Atan(slope.ToDouble()) * (180 / Math.PI);
                continue;
            }
            result[i] = (
                Output switch
                {
                    WindowRegressionOutput.Slope => slope,
                    WindowRegressionOutput.Intercept => mean - slope * center,
                    WindowRegressionOutput.Forecast => mean
                        + slope * (center + new ReferenceFraction(1)),
                    _ => mean + slope * center,
                }
            ).ToDouble();
        }
        return result;
    }

    private sealed class State(int period, WindowRegressionOutput output)
        : IIndicatorState,
            IDisposable
    {
        private readonly ExactLinearFitWindow _fit = new(period, observedHistory: true);

        public void Reset() => _fit.Reset();

        public void Dispose() => _fit.Dispose();

        public double Update(in Bar bar)
        {
            var result = _fit.Next(bar.Close, true);
            return output switch
            {
                WindowRegressionOutput.Forecast => result.Next,
                WindowRegressionOutput.Slope => result.Slope,
                WindowRegressionOutput.Intercept => result.WindowIntercept,
                WindowRegressionOutput.Angle => Math.Atan(result.Slope) * (180 / Math.PI),
                _ => result.Last,
            };
        }
    }
}
