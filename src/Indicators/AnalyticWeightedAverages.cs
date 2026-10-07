using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Fixed Gaussian weights over a rolling close window, with available-prefix normalization.</summary>
/// <remarks>The newest-first coefficient is exp(-((lag-floor(period/2))/floor(period/2)/sigma)^2/2).
/// Coefficients use binary64 elementary functions; their complete weighted ratio rounds once.
/// Gaussian period one is undefined and rejected. Negative sigma is equivalent to positive sigma.
/// If every available coefficient underflows to zero, the prefix publishes zero.
/// Weights and history grow lazily, including when the requested period is very large.</remarks>
public sealed class GaussianWeightedAverage
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a Gaussian window with period at least two and finite nonzero sigma.</summary>
    public GaussianWeightedAverage(int period = 14, double sigma = 1)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(sigma) || sigma == 0) // NOSONAR: Exact zero is the undefined Gaussian width.
            throw new ArgumentOutOfRangeException(nameof(sigma));
        Period = period;
        Sigma = sigma;
    }

    /// <summary>Fixed full-window length.</summary>
    public int Period { get; }

    /// <summary>Gaussian width; its sign does not affect the weights.</summary>
    public double Sigma { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    private double Weight(int lag)
    {
        var center = Period / 2;
        var x = (lag - center) / (double)center / Sigma;
        return Math.Exp(-.5 * x * x);
    }

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new CachedAnalyticKernelState(Period, Weight);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    NormalizedKernelReference.Values(
                        bars,
                        Period,
                        lag => ReferenceFraction.FromDouble(Weight(lag))
                    ),
                0,
                0
            ),
        ];
}

/// <summary>Fixed sine weights over a rolling close window, with available-prefix normalization.</summary>
/// <remarks>The newest-first coefficient is sin((lag+1)*PI/(period+1)), with binary64 PI
/// and elementary functions. The denominator uses wide arithmetic; the complete weighted ratio
/// rounds once. Weights and history grow lazily. A period of one is the identity.</remarks>
public sealed class SineWeightedAverage
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a sine window with a positive period.</summary>
    public SineWeightedAverage(int period = 14)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Fixed full-window length.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    private double Weight(int lag) => Math.Sin((lag + 1d) * Math.PI / (Period + 1d));

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new CachedAnalyticKernelState(Period, Weight);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(
                0,
                bars =>
                    NormalizedKernelReference.Values(
                        bars,
                        Period,
                        lag => ReferenceFraction.FromDouble(Weight(lag))
                    ),
                0,
                0
            ),
        ];
}

internal sealed class CachedAnalyticKernelState : IIndicatorState
{
    private readonly NormalizedKernelState _state;

    internal CachedAnalyticKernelState(int period, Func<int, double> coefficient)
    {
        var weights = new List<double>();
        _state = new NormalizedKernelState(
            period,
            lag =>
            {
                while (weights.Count <= lag)
                    weights.Add(coefficient(weights.Count));
                return weights[lag];
            }
        );
    }

    public void Reset() => _state.Reset();

    public double Update(in Bar bar) => _state.Update(bar);
}
