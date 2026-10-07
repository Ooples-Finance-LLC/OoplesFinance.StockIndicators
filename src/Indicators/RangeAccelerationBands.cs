using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Complete-window averages of accelerated highs, closes and accelerated lows.</summary>
/// <remarks>For high h and low l, acceleration is 4*(h-l)/(h+l), or zero when
/// h+l is exactly zero. Upper inputs are h*(1+acceleration), lower inputs are
/// l*(1-acceleration). Each complete transformed input rounds once with an
/// extended upper exponent; each rolling mean rounds once. Hidden overflow can
/// cancel before publication. A nonfinite selected output is rejected by the
/// runtime. Histories grow with observed input, up to the period.</remarks>
public sealed class RangeAccelerationBands : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates bands requiring a positive complete-window length.</summary>
    public RangeAccelerationBands(int period = 20)
        : base(6)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Required window length.</summary>
    public int Period { get; }

    /// <summary>Mean accelerated high, or zero before startup.</summary>
    public IIndicatorOutput Upper => Outputs[0];

    /// <summary>Mean close, or zero before startup.</summary>
    public IIndicatorOutput Middle => Outputs[1];

    /// <summary>Mean accelerated low, or zero before startup.</summary>
    public IIndicatorOutput Lower => Outputs[2];

    /// <summary>Upper presence.</summary>
    public IIndicatorOutput UpperIsDefined => Outputs[3];

    /// <summary>Middle presence.</summary>
    public IIndicatorOutput MiddleIsDefined => Outputs[4];

    /// <summary>Lower presence.</summary>
    public IIndicatorOutput LowerIsDefined => Outputs[5];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 6)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        RangeAccelerationReference
                            .Values(bars, Period)[slot % 3]
                            .Select(v =>
                                slot < 3 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<BigInteger[]> _window = new();
        private readonly BigInteger[] _sums = new BigInteger[3];

        public void Reset()
        {
            _window.Clear();
            Array.Clear(_sums, 0, 3);
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var h = ExactVarianceWindow.Units(bar.High);
            var l = ExactVarianceWindow.Units(bar.Low);
            var sum = h + l;
            var spread = 4 * (h - l);
            BigInteger[] row = [h, ExactVarianceWindow.Units(bar.Close), l];
            if (!sum.IsZero)
            {
                row[0] = RocBankValue.RoundUnits(
                    h * (sum + spread) * sum.Sign,
                    BigInteger.Abs(sum)
                );
                row[2] = RocBankValue.RoundUnits(
                    l * (sum - spread) * sum.Sign,
                    BigInteger.Abs(sum)
                );
            }
            if (_window.Count == period)
            {
                var oldest = _window.Dequeue();
                for (var j = 0; j < 3; j++)
                    _sums[j] -= oldest[j];
            }
            _window.Enqueue(row);
            for (var j = 0; j < 3; j++)
            {
                _sums[j] += row[j];
                if (_window.Count == period)
                {
                    output[j] = ExactMeanAccumulator.UnitRatio(_sums[j], period);
                    output[j + 3] = 1;
                }
            }
        }
    }
}
