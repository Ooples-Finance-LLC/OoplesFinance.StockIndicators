using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Adjusted sample skewness or unbiased excess kurtosis of a close window.</summary>
/// <remarks>Skewness uses the adjusted Fisher-Pearson coefficient; kurtosis uses
/// the Sheskin sample correction. Available-history startup publishes zero until
/// three or four observations respectively. Flat skewness is zero; mature flat
/// kurtosis is undefined. Exact moments and a once-rounded final result preserve
/// tiny differences and avoid overflowing intermediate powers. History grows lazily.</remarks>
public sealed class WindowSampleShape : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates sample skewness, or excess kurtosis when kurtosis is true.</summary>
    public WindowSampleShape(int period = 20, bool kurtosis = false)
        : base(2)
    {
        if (period < (kurtosis ? 4 : 3))
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        Kurtosis = kurtosis;
    }

    /// <summary>Maximum observations in a window.</summary>
    public int Period { get; }

    /// <summary>Whether to calculate excess kurtosis instead of skewness.</summary>
    public bool Kurtosis { get; }

    /// <summary>Statistic, or zero when undefined.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One for a defined value, including the explicit zero startup convention.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Kurtosis);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars =>
                        SampleShapeReference
                            .Calculate(bars, Period, Kurtosis)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, bool kurtosis) : IMultiOutputState
    {
        private readonly Queue<double> _history = new();
        private BigInteger _s1,
            _s2,
            _s3,
            _s4;

        public void Reset()
        {
            _history.Clear();
            _s1 = _s2 = _s3 = _s4 = 0;
        }

        private void Add(double price, int sign)
        {
            var x = ExactVarianceWindow.Units(price);
            var x2 = x * x;
            _s1 += sign * x;
            _s2 += sign * x2;
            _s3 += sign * x2 * x;
            if (kurtosis)
                _s4 += sign * x2 * x2;
        }

        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_history.Count == period)
                Add(_history.Dequeue(), -1);
            _history.Enqueue(bar.Close);
            Add(bar.Close, 1);
            outputs[0] = 0;
            outputs[1] = 1;
            if (_history.Count < (kurtosis ? 4 : 3))
                return;
            var n = new BigInteger(_history.Count);
            var a = n * _s2 - _s1 * _s1;
            if (a.IsZero)
            {
                if (kurtosis)
                    outputs[1] = 0;
                return;
            }
            if (kurtosis)
            {
                var c =
                    n * n * n * _s4
                    - 4 * n * n * _s1 * _s3
                    + 6 * n * _s1 * _s1 * _s2
                    - 3 * BigInteger.Pow(_s1, 4);
                var top = (n * n - 1) * c - 3 * (n - 1) * (n - 1) * a * a;
                outputs[0] = ExactMeanAccumulator.UnitRatio(top << 1074, a * a * (n - 2) * (n - 3));
            }
            else
            {
                var b = n * n * _s3 - 3 * n * _s1 * _s2 + 2 * _s1 * _s1 * _s1;
                outputs[0] =
                    b.Sign
                    * ExactPopulationDeviation.RootRatio(
                        (b * b * n * (n - 1)) << 2148,
                        a * a * a * (n - 2) * (n - 2)
                    );
            }
        }
    }
}
