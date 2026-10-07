using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Startup and exact-zero state convention for McGinley Dynamic.</summary>
public enum McGinleyStartup
{
    /// <summary>Publish the first close; a zero previous value uses a ratio of one.</summary>
    Immediate,

    /// <summary>Omit the first result and restart a period-long delay whenever the previous value is zero.</summary>
    ResetDelay,
}

/// <summary>McGinley Dynamic with an exactly evaluated quartic recurrence.</summary>
/// <remarks>Each complete recurrence m+(price-m)/(factor*period*(price/m)^4)
/// rounds once, avoiding overflowing differences, powers and products.
/// Immediate mode substitutes ratio one when m is zero; reset-delay mode resets
/// m to price and restarts the output delay. Zero price with nonzero m is singular.
/// Singular or overflowing recurrence states are rejected, including during a
/// restarted delay. State size does not depend on the period.</remarks>
public sealed class QuarticDynamicAverage : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a dynamic average with positive period and finite positive factor.</summary>
    public QuarticDynamicAverage(
        int period = 14,
        double factor = .6,
        McGinleyStartup startup = McGinleyStartup.Immediate
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!double.IsFinite(factor) || factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor));
        if (startup is not (McGinleyStartup.Immediate or McGinleyStartup.ResetDelay))
            throw new ArgumentOutOfRangeException(nameof(startup));
        Period = period;
        Factor = factor;
        Startup = startup;
    }

    /// <summary>Recurrence period and restart delay.</summary>
    public int Period { get; }

    /// <summary>Positive recurrence scale.</summary>
    public double Factor { get; }

    /// <summary>Startup and zero-state convention.</summary>
    public McGinleyStartup Startup { get; }

    /// <summary>Dynamic average or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the value is present.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period, Factor, Startup);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        McGinleyReference
                            .Values(bars, Period, Factor, Startup)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period, double factor, McGinleyStartup startup)
        : IMultiOutputState
    {
        private long _index = -1,
            _start = 1;
        private double _previous;
        private readonly BigInteger _scale = ExactVarianceWindow.Units(factor) * period;

        public void Reset()
        {
            _index = -1;
            _start = 1;
            _previous = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            var value = bar.Close;
            if (++_index == 0)
            {
                _previous = value;
                if (startup == McGinleyStartup.Immediate)
                {
                    output[0] = value;
                    output[1] = 1;
                    return;
                }
            }
            if (startup == McGinleyStartup.ResetDelay && _previous == 0) // NOSONAR: Exact zero triggers the restart.
            {
                _previous = value;
                _start = _index + period;
                return;
            }
            var price = ExactVarianceWindow.Units(value);
            var old = ExactVarianceWindow.Units(_previous);
            double next;
            if (old.IsZero)
                next = ExactMeanAccumulator.UnitRatio(price << 1074, _scale);
            else if (price.IsZero)
                next = old.Sign > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            else
            {
                var power = BigInteger.Pow(price, 4);
                var divisor = _scale * power;
                next = ExactMeanAccumulator.UnitRatio(
                    old * divisor + ((price - old) * BigInteger.Pow(old, 4) << 1074),
                    divisor
                );
            }
            if (!double.IsFinite(next))
            {
                output[0] = next;
                return;
            }
            _previous = next;
            if (startup == McGinleyStartup.Immediate || _index >= _start)
            {
                output[0] = next;
                output[1] = 1;
            }
        }
    }
}
