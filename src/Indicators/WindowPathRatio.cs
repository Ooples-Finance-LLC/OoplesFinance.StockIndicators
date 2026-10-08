using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Signed Chande percentage or absolute efficiency fraction.</summary>
public enum PathRatioConvention
{
    SignedPercent,
    AbsoluteFraction,
}

/// <summary>Net close movement divided by the sum of absolute one-bar movements.</summary>
/// <remarks>A complete period of changes is required; flat windows are absent.
/// Differences, path sums and the final ratio use exact arithmetic until final
/// binary64 rounding. History grows lazily, bounded by the period.</remarks>
public sealed class WindowPathRatio : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period ratio with the chosen sign and scale.</summary>
    public WindowPathRatio(
        int period = 14,
        PathRatioConvention convention = PathRatioConvention.SignedPercent
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(convention.GetType(), convention))
            throw new ArgumentOutOfRangeException(nameof(convention));
        Period = period;
        Convention = convention;
    }

    /// <summary>Number of one-bar changes in the window.</summary>
    public int Period { get; }

    /// <summary>Signed percentage or absolute fraction.</summary>
    public PathRatioConvention Convention { get; }

    /// <summary>Ratio, or zero while absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the ratio is defined; otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Convention);

    /// <summary>Computes ratios from nullable values, preserving enumeration order.</summary>
    /// <remarks>Requires both window endpoints. Missing adjacent pairs contribute
    /// no path distance, matching the nullable efficiency convention. Consequently
    /// gaps can yield a fraction above one. Nonfinite inputs and unrepresentable
    /// results are rejected; each enumeration starts with fresh history.</remarks>
    public static IEnumerable<double?> FromValues(
        IEnumerable<double?> values,
        int period = 14,
        PathRatioConvention convention = PathRatioConvention.AbsoluteFraction
    )
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        var specification = new WindowPathRatio(period, convention);
        return Enumerate();
        IEnumerable<double?> Enumerate()
        {
            var calculator = new Calculator(specification.Period, specification.Convention);
            foreach (var value in values)
            {
                if (value.HasValue && !FrameworkCompatibility.IsFinite(value.Value))
                    throw new ArgumentOutOfRangeException(nameof(values));
                var result = calculator.Next(value);
                if (result.HasValue && !FrameworkCompatibility.IsFinite(result.Value))
                    throw new ArithmeticException("Path ratio is not representable.");
                yield return result;
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var output = new[] { new double[bars.Count], new double[bars.Count] };
        for (var i = Period; i < bars.Count; i++)
        {
            var distance = new ReferenceFraction(0);
            for (var j = i - Period + 1; j <= i; j++)
            {
                var a = bars[j].Close;
                var b = bars[j - 1].Close;
                distance +=
                    ReferenceFraction.FromDouble(Math.Max(a, b))
                    - ReferenceFraction.FromDouble(Math.Min(a, b));
            }
            if (distance.Components.Numerator.IsZero)
                continue;
            var numerator =
                ReferenceFraction.FromDouble(bars[i].Close)
                - ReferenceFraction.FromDouble(bars[i - Period].Close);
            var ratio = (
                numerator
                * (new ReferenceFraction(Convention == PathRatioConvention.SignedPercent ? 100 : 1))
                / distance
            ).ToDouble();
            output[0][i] =
                Convention == PathRatioConvention.AbsoluteFraction ? Math.Abs(ratio) : ratio;
            output[1][i] = 1;
        }
        return output;
    }

    private sealed class Calculator(int period, PathRatioConvention convention)
    {
        private readonly Queue<double?> _prices = new();
        private readonly Queue<ExactMeanAccumulator> _steps = new();
        private ExactMeanAccumulator _path;
        private double? _previous;

        internal void Reset()
        {
            _prices.Clear();
            _steps.Clear();
            _path = default;
            _previous = null;
        }

        internal double? Next(double? value)
        {
            var step = new ExactMeanAccumulator();
            if (value.HasValue && _previous.HasValue)
            {
                step.Add(Math.Max(value.Value, _previous.Value));
                step.Add(Math.Min(value.Value, _previous.Value), -1);
            }
            if (_steps.Count == period)
                _path.Subtract(_steps.Dequeue());
            _steps.Enqueue(step);
            _path.AddExact(step);
            _previous = value;
            var mature = _prices.Count == period;
            var first = mature ? _prices.Dequeue() : null;
            _prices.Enqueue(value);
            if (!mature || !value.HasValue || !first.HasValue || _path.IsExactlyZero)
                return null;
            var numerator = new ExactMeanAccumulator();
            var scale = convention == PathRatioConvention.SignedPercent ? 100 : 1;
            numerator.Add(value.Value, scale);
            numerator.Add(first.Value, -scale);
            var ratio = numerator.Ratio(_path);
            return convention == PathRatioConvention.AbsoluteFraction ? Math.Abs(ratio) : ratio;
        }
    }

    private sealed class State(int period, PathRatioConvention convention) : IMultiOutputState
    {
        private readonly Calculator _calculator = new(period, convention);

        public void Reset() => _calculator.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            var value = _calculator.Next(bar.Close);
            output[0] = value ?? 0;
            output[1] = value.HasValue ? 1 : 0;
        }
    }
}
