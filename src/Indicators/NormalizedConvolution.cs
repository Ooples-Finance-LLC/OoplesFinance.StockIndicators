using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Rolling weighted close filter with weights ordered newest first.</summary>
/// <remarks>Each available prefix of the kernel is normalized by its exact sum. If that sum
/// is zero, divide the weighted response by the number of available observations instead.
/// The kernel is copied on construction. The complete weighted ratio is rounded once.</remarks>
public sealed class NormalizedConvolution : IndicatorBase, IIndicatorValidationContract
{
    private readonly double[] _kernel;
    private readonly int _firstWeight, _weightStep;
    internal bool HasAffineKernel { get; }

    /// <summary>Creates a filter from a nonempty finite newest-first kernel.</summary>
    public NormalizedConvolution(IEnumerable<double> kernel)
    {
        if (kernel is null) throw new ArgumentNullException(nameof(kernel));
        _kernel = kernel.ToArray();
        if (_kernel.Length == 0 || _kernel.Any(v => !FrameworkCompatibility.IsFinite(v)))
            throw new ArgumentException(
                "A kernel must contain at least one finite weight and no nonfinite weights.",
                nameof(kernel)
            );
        Kernel = Array.AsReadOnly(_kernel);
        HasAffineKernel = TryIntegerProgression(_kernel, out _firstWeight, out _weightStep);
    }

    /// <summary>Immutable newest-first weights supplied at construction.</summary>
    public IReadOnlyList<double> Kernel { get; }

    /// <inheritdoc/>
    public override int WarmupBars => _kernel.Length - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => HasAffineKernel
        ? new AffineConvolutionState(_kernel.Length, _firstWeight, _weightStep)
        : new NormalizedKernelState(_kernel.Length, lag => _kernel[lag]);

    private static bool TryIntegerProgression(double[] kernel, out int first, out int step)
    {
        first = step = 0;
        if (kernel[0] < int.MinValue || kernel[0] > int.MaxValue || kernel[0] != Math.Truncate(kernel[0])) return false;
        first = (int)kernel[0];
        if (kernel.Length == 1) return true;
        double difference = kernel[1] - first;
        if (difference < int.MinValue || difference > int.MaxValue || difference != Math.Truncate(difference)) return false;
        step = (int)difference;
        for (int i = 1; i < kernel.Length; i++)
        {
            long expected = first + (long)step * i;
            if (expected < int.MinValue || expected > int.MaxValue || kernel[i] != expected) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars =>
                    NormalizedKernelReference.Values(
                        bars,
                        _kernel.Length,
                        lag => ReferenceFraction.FromDouble(_kernel[lag])
                    ),
                IndicatorErrorBudget.Exact
            ),
        ];
}

/// <summary>Endpoint regression weights with fixed full-period coefficients during startup.</summary>
/// <remarks>The newest-first integer weight at lag k is 2*period-1-3*k. Available prefixes
/// are normalized by their sum. A complete window equals its fitted regression endpoint,
/// but startup retains the requested period's weights rather than fitting a shorter line.
/// The optional startup mean replaces those partial weighted responses. History grows lazily;
/// rolling exact moments avoid scanning the window and each output is rounded once.</remarks>
public sealed class EndpointWeightedAverage
    : IndicatorBase,
        IMovingAverage,
        IIndicatorValidationContract
{
    /// <summary>Creates a positive-period endpoint-weighted average.</summary>
    public EndpointWeightedAverage(int period = 14)
        : this(period, false) { }

    /// <summary>Creates endpoint weights with an optional arithmetic mean during incomplete windows.</summary>
    public EndpointWeightedAverage(int period, bool averageDuringWarmup)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        AverageDuringWarmup = averageDuringWarmup;
    }

    /// <summary>Full coefficient window length.</summary>
    public int Period { get; }

    /// <summary>Whether incomplete windows publish their arithmetic mean.</summary>
    public bool AverageDuringWarmup { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new EndpointState(Period, AverageDuringWarmup);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.ReferenceWithOverflowRejection(
                0,
                bars =>
                    NormalizedKernelReference.Values(
                        bars,
                        Period,
                        lag => new ReferenceFraction(2L * Period - 1 - 3L * lag),
                        AverageDuringWarmup
                    ),
                IndicatorErrorBudget.Exact
            ),
        ];

    private sealed class EndpointState(int period, bool averageDuringWarmup)
        : IIndicatorState,
            IDisposable
    {
        private readonly ExactLinearFitWindow _fit = new(period, observedHistory: true, compact: true);

        public void Reset() => _fit.Reset();

        public void Dispose() => _fit.Dispose();

        public double Update(in Bar bar) =>
            _fit.Next(bar.Close, true).EndpointWeights(period, averageDuringWarmup);
    }
}

// Exact FIR recurrence for integer arithmetic progressions, newest coefficient first.
// Shifting the old window adds step*sum; the evicted sample then has coefficient
// first+step*length. Partial windows retain the same coefficients and exact mass.
internal sealed class AffineConvolutionState(int length, int first, int step) : IIndicatorState
{
    private readonly Queue<double> _history = new();
    private ExactMeanAccumulator _sum, _weighted;
    private long _mass;
    private readonly long _evictionWeight = -(first + (long)step * length);

    public void Reset()
    {
        _history.Clear();
        _sum = _weighted = default;
        _mass = 0;
    }

    public double Update(in Bar bar)
    {
        if (step == 1) _weighted.AddExact(_sum);
        else if (step == -1) _weighted.Subtract(_sum);
        else if (step != 0)
        {
            var shift = _sum;
            shift.Multiply(step);
            _weighted.AddExact(shift);
        }
        if (_history.Count == length)
        {
            double oldest = _history.Dequeue();
            if (_evictionWeight is >= int.MinValue and <= int.MaxValue)
                _weighted.Add(oldest, (int)_evictionWeight);
            else _weighted.Add(oldest, new System.Numerics.BigInteger(_evictionWeight));
            _sum.Add(oldest, -1);
        }
        else _mass += first + (long)step * _history.Count;
        _history.Enqueue(bar.Close);
        _sum.Add(bar.Close);
        _weighted.Add(bar.Close, first);
        if (_mass == 0) return _weighted.Mean(_history.Count);
        // The mass fits long: every retained coefficient is an Int32 and there
        // are at most Int32.MaxValue of them. Preserve the existing zero sign.
        if (_mass > 0) return _weighted.Mean(_mass);
        if (_weighted.IsExactlyZero) return 0;
        return -_weighted.Mean(-_mass);
    }
}

internal sealed class NormalizedKernelState(int length, Func<int, double> weight) : IIndicatorState
{
    private readonly Queue<ExactMeanAccumulator.ProductFactor> _history = new();
    private readonly List<ExactMeanAccumulator.ProductCoefficient> _coefficients = new();
    private ExactMeanAccumulator _denominator;

    public void Reset()
    {
        _history.Clear();
        _denominator = default;
    }

    public double Update(in Bar bar)
    {
        if (_history.Count == length)
            _history.Dequeue();
        else
        {
            // These internal kernels have fixed newest-first coefficients. Only
            // startup extends the coefficient prefix; eviction does not change it.
            if (_coefficients.Count == _history.Count)
                _coefficients.Add(new ExactMeanAccumulator.ProductCoefficient(weight(_history.Count)));
            _denominator.AddFactor(_coefficients[_history.Count].Factor);
        }
        _history.Enqueue(new ExactMeanAccumulator.ProductFactor(bar.Close));
        var numerator = new ExactMeanAccumulator();
        var lag = _history.Count;
        foreach (var price in _history)
        {
            var coefficient = _coefficients[--lag];
            numerator.AddProduct(in price, in coefficient);
        }
        return _denominator.IsExactlyZero
            ? numerator.Mean(_history.Count)
            : numerator.Ratio(_denominator);
    }
}

internal static class NormalizedKernelReference
{
    internal static IReadOnlyList<double> Values(
        IReadOnlyList<Bar> bars,
        int length,
        Func<int, ReferenceFraction> weight,
        bool averageDuringWarmup = false
    )
    {
        var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(i + 1, length);
            var total = new ReferenceFraction(0);
            var mass = new ReferenceFraction(0);
            for (var lag = 0; lag < count; lag++)
            {
                var coefficient =
                    averageDuringWarmup && count < length ? new ReferenceFraction(1) : weight(lag);
                total += coefficient * ReferenceFraction.FromDouble(bars[i - lag].Close);
                mass += coefficient;
            }
            result[i] = (total / (mass.Sign == 0 ? new ReferenceFraction(count) : mass)).ToDouble();
        }
        return result;
    }
}
