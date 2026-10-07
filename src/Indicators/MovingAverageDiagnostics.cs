using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Mean absolute distance from the once-rounded mean of the available rolling window.</summary>
/// <remarks>All differences and their average are exact before final rounding. History
/// grows lazily. Only absolute deviation is published, so oversized squared or relative
/// errors cannot invalidate this scalar output.</remarks>
public sealed class WindowMeanAbsoluteDeviation : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates mean absolute deviation for a positive window size.</summary>
    public WindowMeanAbsoluteDeviation(int period = 20)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Maximum number of observed closes in the window.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars => MeanErrorReference.Values(bars, Period, false)[1],
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class State(int period) : IIndicatorState
    {
        private readonly RoundedMeanErrorWindow _window = new(period);

        public void Reset() => _window.Reset();

        public double Update(in Bar bar)
        {
            _window.Add(bar.Close);
            return _window.AbsoluteError(_window.Mean);
        }
    }
}

/// <summary>Published rolling mean and its absolute, squared, and signed relative absolute errors.</summary>
/// <remarks>All errors use the published mean as their center. SignedRelativeError is
/// average(abs(close-Mean)/close), without a factor of 100 and with signed denominators.
/// It is absent when any window price is zero. All four outputs start with a full
/// window, use zero placeholders while absent, and have explicit presence flags.
/// Exact intermediate arithmetic preserves finite cancellation. The runtime rejects
/// unrepresentable published errors. History grows lazily.</remarks>
public sealed class MovingAverageDiagnostics
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates full-window mean diagnostics for a positive period.</summary>
    public MovingAverageDiagnostics(int period = 20)
        : base(8)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Number of closes required before publishing diagnostics.</summary>
    public int Period { get; }

    /// <summary>Exact average rounded once to binary64, and the center of all error readings.</summary>
    public IIndicatorOutput Mean => Outputs[0];

    /// <summary>Average absolute distance from the published mean.</summary>
    public IIndicatorOutput MeanAbsoluteError => Outputs[1];

    /// <summary>Average squared distance from the published mean.</summary>
    public IIndicatorOutput MeanSquaredError => Outputs[2];

    /// <summary>Average abs(close-Mean)/close, using signed denominators and no percentage multiplier.</summary>
    public IIndicatorOutput SignedRelativeError => Outputs[3];

    /// <summary>One when the price window is complete.</summary>
    public IIndicatorOutput MeanIsDefined => Outputs[4];

    /// <summary>One when the price window is complete.</summary>
    public IIndicatorOutput MeanAbsoluteErrorIsDefined => Outputs[5];

    /// <summary>One when the price window is complete.</summary>
    public IIndicatorOutput MeanSquaredErrorIsDefined => Outputs[6];

    /// <summary>One when the price window is complete and contains no zero close.</summary>
    public IIndicatorOutput SignedRelativeErrorIsDefined => Outputs[7];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Mean;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 8)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars => MeanErrorReference.Values(bars, Period, true)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly RoundedMeanErrorWindow _window = new(period);

        public void Reset() => _window.Reset();

        public void Update(in Bar bar, Span<double> outputs)
        {
            outputs.Clear();
            _window.Add(bar.Close);
            if (_window.Count < period)
                return;
            var mean = _window.Mean;
            outputs[0] = mean;
            outputs[1] = _window.AbsoluteError(mean);
            outputs[2] = _window.SquaredError(mean);
            outputs[4] = outputs[5] = outputs[6] = 1;
            var relative = _window.SignedRelativeError(mean);
            outputs[3] = relative.Value;
            outputs[7] = relative.Defined ? 1 : 0;
        }
    }
}

internal static class MeanErrorReference
{
    internal static double[][] Values(IReadOnlyList<Bar> bars, int period, bool details)
    {
        var results = Enumerable.Range(0, 8).Select(_ => new double[bars.Count]).ToArray();
        for (var i = details ? period - 1 : 0; i < bars.Count; i++)
        {
            var count = Math.Min(period, i + 1);
            var values = bars.Skip(i - count + 1)
                .Take(count)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var n = new ReferenceFraction(count);
            var mean = (values.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n).ToDouble();
            var center = ReferenceFraction.FromDouble(mean);
            var distances = values.Select(v => (v - center).Abs()).ToArray();
            results[1][i] = (
                distances.Aggregate(new ReferenceFraction(0), (a, b) => a + b) / n
            ).ToDouble();
            if (!details)
                continue;
            results[0][i] = mean;
            results[2][i] = (
                distances.Aggregate(new ReferenceFraction(0), (a, b) => a + b * b) / n
            ).ToDouble();
            results[4][i] = results[5][i] = results[6][i] = 1;
            if (values.Any(v => v.Sign == 0))
                continue;
            var relative = new ReferenceFraction(0);
            for (var j = 0; j < count; j++)
                relative += distances[j] / values[j];
            results[3][i] = (relative / n).ToDouble();
            results[7][i] = 1;
        }
        return results;
    }
}
