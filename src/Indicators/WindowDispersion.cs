using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Selected statistic of a rolling close distribution.</summary>
public enum WindowDispersionOutput
{
    /// <summary>Mean squared distance from the current window's mean.</summary>
    Variance,

    /// <summary>Square root of the current window's variance.</summary>
    StandardDeviation,

    /// <summary>Latest close's distance from the current window's mean, divided by its deviation.</summary>
    ZScore,
}

/// <summary>Sample or population rolling dispersion with exact moments and available-history startup.</summary>
/// <remarks>Sample statistics divide squared deviations by count-1; population statistics
/// divide by count. One observation produces zero. A constant window has zero Z-score.
/// The optional multiplier scales the selected mathematical result before final rounding,
/// preserving finite results across unrepresentable intermediate variance or deviation.
/// Every window uses its own mean. History grows lazily up to the requested period.</remarks>
public sealed class WindowDispersion : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a rolling dispersion calculation.</summary>
    /// <param name="period">Positive maximum window size.</param>
    /// <param name="output">Variance, deviation, or latest close's Z-score.</param>
    /// <param name="sample">Whether to use count-1 instead of count in the variance denominator.</param>
    /// <param name="multiplier">Finite signed scale applied before final rounding.</param>
    public WindowDispersion(
        int period = 20,
        WindowDispersionOutput output = WindowDispersionOutput.StandardDeviation,
        bool sample = false,
        double multiplier = 1
    )
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(output))
            throw new ArgumentOutOfRangeException(nameof(output));
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        Period = period;
        Output = output;
        Sample = sample;
        Multiplier = multiplier;
    }

    /// <summary>Maximum observations in the current window.</summary>
    public int Period { get; }

    /// <summary>Selected statistic.</summary>
    public WindowDispersionOutput Output { get; }

    /// <summary>Whether squared deviations use the sample denominator count-1.</summary>
    public bool Sample { get; }

    /// <summary>Signed scale applied to the mathematical statistic before final rounding.</summary>
    public double Multiplier { get; }

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Output, Sample, Multiplier);

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
        var scale = ReferenceFraction.FromDouble(Multiplier);
        for (var i = 0; i < bars.Count; i++)
        {
            var count = Math.Min(Period, i + 1);
            if (count < 2)
                continue;
            var values = bars.Skip(i - count + 1)
                .Take(count)
                .Select(b => ReferenceFraction.FromDouble(b.Close))
                .ToArray();
            var mean =
                values.Aggregate(new ReferenceFraction(0), (a, b) => a + b)
                / new ReferenceFraction(count);
            var variance =
                values.Aggregate(new ReferenceFraction(0), (a, b) => a + (b - mean) * (b - mean))
                / new ReferenceFraction(Sample ? count - 1 : count);
            if (Output == WindowDispersionOutput.Variance)
                result[i] = (variance * scale).ToDouble();
            else if (Output == WindowDispersionOutput.StandardDeviation)
                result[i] = scale.Sign * (variance * scale * scale).SqrtToDouble();
            else if (variance.Sign != 0)
            {
                var deviation = (values[^1] - mean) * scale;
                result[i] = deviation.Sign * (deviation * deviation / variance).SqrtToDouble();
            }
        }
        return result;
    }

    internal sealed class State(
        int period,
        WindowDispersionOutput output,
        bool sample,
        double multiplier
    ) : IIndicatorState
    {
        private readonly Queue<double> _history = new();
        private readonly BigInteger _scale = ExactVarianceWindow.Units(multiplier);
        private BigInteger _sum,
            _squares,
            _current;

        internal int Count => _history.Count;
        internal double Mean => Count == 0 ? 0 : ExactMeanAccumulator.UnitRatio(_sum, Count);
        internal bool HasVariance => Count > 1 && Count * _squares > _sum * _sum;

        public void Reset()
        {
            _history.Clear();
            _sum = _squares = _current = 0;
        }

        public double Update(in Bar bar)
        {
            var value = ExactVarianceWindow.Units(bar.Close);
            if (_history.Count == period)
            {
                var expired = ExactVarianceWindow.Units(_history.Dequeue());
                _sum -= expired;
                _squares -= expired * expired;
            }
            _history.Enqueue(bar.Close);
            _sum += value;
            _squares += value * value;
            _current = value;
            return Reading(output);
        }

        internal double Reading(WindowDispersionOutput selected)
        {
            if (_history.Count < 2 || _scale.IsZero)
                return 0;
            var n = new BigInteger(_history.Count);
            var variance = n * _squares - _sum * _sum;
            var denominator = n * (sample ? n - 1 : n);
            if (selected == WindowDispersionOutput.Variance)
                return ExactMeanAccumulator.UnitRatio(variance * _scale, denominator << 2148);
            if (selected == WindowDispersionOutput.StandardDeviation)
                return _scale.Sign
                    * ExactPopulationDeviation.RootRatio(
                        variance * _scale * _scale,
                        denominator << 2148
                    );
            if (variance.IsZero)
                return 0;
            var deviation = (n * _current - _sum) * _scale;
            return deviation.Sign
                * ExactPopulationDeviation.RootRatio(
                    deviation * deviation * denominator,
                    n * n * variance
                );
        }
    }
}
