using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Sum-seeded directional indicators, DX, ADX and period-delayed ADXR.</summary>
/// <remarks>Movement selection uses exact differences. Selected movement, true range,
/// initial sums and each Wilder sum recurrence round once with extended upper
/// exponents. DI ratios round before DX. DI and DX start at index period; ADX
/// seeds at index 2*period-1 and ADXR uses a period-bar lag. Zero smoothed range
/// makes all outputs absent and skips ADX updating. A skipped ADX seed is never
/// recovered. Missing DX before the seed contributes zero to its fixed divisor.
/// History grows lazily and derived lookbacks use 64-bit arithmetic.</remarks>
public sealed class SumSeededDirectionalIndex
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates directional outputs with a period of at least two.</summary>
    public SumSeededDirectionalIndex(int period = 14)
        : base(10)
    {
        if (period < 2)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Smoothing period and rating lag.</summary>
    public int Period { get; }

    /// <summary>Positive directional indicator, or zero when absent.</summary>
    public IIndicatorOutput PositiveIndicator => Outputs[0];

    /// <summary>Negative directional indicator, or zero when absent.</summary>
    public IIndicatorOutput NegativeIndicator => Outputs[1];

    /// <summary>Directional movement index, or zero when absent.</summary>
    public IIndicatorOutput Index => Outputs[2];

    /// <summary>Average directional index, or zero when absent.</summary>
    public IIndicatorOutput Average => Outputs[3];

    /// <summary>Average directional index rating, or zero when absent.</summary>
    public IIndicatorOutput Rating => Outputs[4];

    /// <summary>Positive directional indicator presence.</summary>
    public IIndicatorOutput PositiveIsDefined => Outputs[5];

    /// <summary>Negative directional indicator presence.</summary>
    public IIndicatorOutput NegativeIsDefined => Outputs[6];

    /// <summary>Directional index presence.</summary>
    public IIndicatorOutput IndexIsDefined => Outputs[7];

    /// <summary>Average directional index presence.</summary>
    public IIndicatorOutput AverageIsDefined => Outputs[8];

    /// <summary>Rating presence.</summary>
    public IIndicatorOutput RatingIsDefined => Outputs[9];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 10)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        SumDirectionalReference
                            .Calculate(bars, Period)[slot % 5]
                            .Select(v =>
                                slot < 5 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<double?> _history = new();
        private Bar _previous;
        private long _index = -1;
        private BigInteger _positive,
            _negative,
            _range,
            _seedDx;
        private double? _average;

        public void Reset()
        {
            _history.Clear();
            _previous = default;
            _index = -1;
            _positive = _negative = _range = _seedDx = 0;
            _average = null;
        }

        private static BigInteger U(double x) => ExactVarianceWindow.Units(x);

        private static BigInteger R(BigInteger x, BigInteger divisor) =>
            RocBankValue.RoundUnits(x, divisor);

        private static void Publish(double? value, int slot, Span<double> output)
        {
            if (value.HasValue)
            {
                output[slot] = value.Value;
                output[slot + 5] = 1;
            }
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var prior = _history.Count == period ? _history.Dequeue() : null;
            double? averageOutput = null;
            if (++_index > 0)
            {
                var up = U(bar.High) - U(_previous.High);
                var down = U(_previous.Low) - U(bar.Low);
                var positive = R(up > 0 && up > down ? up : BigInteger.Zero, 1);
                var negative = R(down > 0 && down > up ? down : BigInteger.Zero, 1);
                var range = R(
                    BigInteger.Max(
                        U(bar.High) - U(bar.Low),
                        BigInteger.Max(
                            BigInteger.Abs(U(bar.High) - U(_previous.Close)),
                            BigInteger.Abs(U(bar.Low) - U(_previous.Close))
                        )
                    ),
                    1
                );
                if (_index <= period)
                {
                    _positive += positive;
                    _negative += negative;
                    _range += range;
                    if (_index == period)
                    {
                        _positive = R(_positive, 1);
                        _negative = R(_negative, 1);
                        _range = R(_range, 1);
                    }
                }
                else
                {
                    _positive = R(_positive * (period - 1) + positive * period, period);
                    _negative = R(_negative * (period - 1) + negative * period, period);
                    _range = R(_range * (period - 1) + range * period, period);
                }
                if (_index >= period && !_range.IsZero)
                {
                    var pdi = R((100 * _positive) << 1074, _range);
                    var mdi = R((100 * _negative) << 1074, _range);
                    var dx = (pdi + mdi).IsZero
                        ? 0
                        : ExactMeanAccumulator.UnitRatio(
                            (100 * BigInteger.Abs(pdi - mdi)) << 1074,
                            pdi + mdi
                        );
                    Publish(ExactMeanAccumulator.UnitRatio(pdi, 1), 0, output);
                    Publish(ExactMeanAccumulator.UnitRatio(mdi, 1), 1, output);
                    Publish(dx, 2, output);
                    var seedIndex = 2L * period - 1;
                    if (_index <= seedIndex)
                    {
                        _seedDx += U(dx);
                        if (_index == seedIndex)
                            _average = ExactMeanAccumulator.UnitRatio(_seedDx, period);
                    }
                    else if (_average.HasValue)
                        _average = ExactMeanAccumulator.UnitRatio(
                            U(_average.Value) * (period - 1) + U(dx),
                            period
                        );
                    averageOutput = _average;
                    Publish(averageOutput, 3, output);
                    if (prior.HasValue && averageOutput.HasValue)
                        Publish(
                            ExactMeanAccumulator.UnitRatio(
                                U(prior.Value) + U(averageOutput.Value),
                                2
                            ),
                            4,
                            output
                        );
                }
            }
            _previous = bar;
            _history.Enqueue(averageOutput);
        }
    }
}
