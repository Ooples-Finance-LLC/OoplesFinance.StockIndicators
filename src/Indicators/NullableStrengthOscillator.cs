using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Trady strength and momentum formulas with their explicit missing-output conventions.</summary>
public enum NullableStrengthConvention
{
    RelativeStrength,
    RelativeStrengthIndex,
    NetMomentum,
    RelativeMomentum,
    RelativeMomentumIndex,
}

/// <summary>SMA-seeded gain/loss ratios with nullable strength and momentum conventions.</summary>
/// <remarks>Strength uses Wilder smoothing and one-bar changes; momentum uses
/// EMA smoothing and configurable lagged changes. Loss zero makes all results absent;
/// Momentum Index also requires nonzero gain. Unpublished averages retain 53 binary
/// digits with extended exponents. Each final formula rounds once. Lag history grows lazily.</remarks>
public sealed class NullableStrengthOscillator
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates the chosen positive-period formula; momentum variants accept a positive lag.</summary>
    public NullableStrengthOscillator(
        int period = 14,
        NullableStrengthConvention convention = NullableStrengthConvention.RelativeStrengthIndex,
        int momentumPeriod = 1
    )
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (!Enum.IsDefined(convention.GetType(), convention))
            throw new ArgumentOutOfRangeException(nameof(convention));
        if (momentumPeriod < 1 || !IsMomentum(convention) && momentumPeriod != 1)
            throw new ArgumentOutOfRangeException(nameof(momentumPeriod));
        Period = period;
        Convention = convention;
        MomentumPeriod = momentumPeriod;
    }

    /// <summary>Smoothing and seed length.</summary>
    public int Period { get; }

    /// <summary>Selected formula and absence rules.</summary>
    public NullableStrengthConvention Convention { get; }

    /// <summary>Change lag, fixed to one for strength variants.</summary>
    public int MomentumPeriod { get; }

    /// <summary>Formula value, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the selected formula is defined; otherwise zero.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    internal static bool IsMomentum(NullableStrengthConvention convention) =>
        convention
            is NullableStrengthConvention.RelativeMomentum
                or NullableStrengthConvention.RelativeMomentumIndex;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new State(Period, Convention, MomentumPeriod);

    /// <summary>Computes nullable inputs in enumeration order with independent state per enumeration.</summary>
    /// <remarks>The initial average ignores missing changes. If no seed change exists,
    /// or a later change is missing, subsequent results remain absent. Nonfinite inputs
    /// and unrepresentable final ratios are rejected. Bounded index formulas never
    /// publish an intermediate unbounded ratio.</remarks>
    public static IEnumerable<double?> FromValues(
        IEnumerable<double?> values,
        int period = 14,
        NullableStrengthConvention convention = NullableStrengthConvention.RelativeStrengthIndex,
        int momentumPeriod = 1
    )
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        var specification = new NullableStrengthOscillator(period, convention, momentumPeriod);
        return Enumerate();
        IEnumerable<double?> Enumerate()
        {
            var calculator = new Calculator(
                specification.Period,
                specification.Convention,
                specification.MomentumPeriod
            );
            foreach (var value in values)
            {
                if (value.HasValue && !FrameworkCompatibility.IsFinite(value.Value))
                    throw new ArgumentOutOfRangeException(nameof(values));
                var result = calculator.Next(value);
                if (result.HasValue && !FrameworkCompatibility.IsFinite(result.Value))
                    throw new ArithmeticException("Strength ratio is not representable.");
                yield return result;
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        NullableStrengthReference
                            .Calculate(
                                bars.Select(b => (double?)b.Close).ToArray(),
                                Period,
                                Convention,
                                MomentumPeriod
                            )
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    internal sealed class Calculator(int period, NullableStrengthConvention convention, int lag)
    {
        private readonly Queue<double?> _history = new();
        private int _seedSteps,
            _known;
        private ExactMeanAccumulator _gainSeed,
            _lossSeed;
        private PositiveStrengthValue _gain,
            _loss;
        private bool _poisoned;

        internal void Reset()
        {
            _history.Clear();
            _seedSteps = 0;
            _known = 0;
            _gainSeed = default;
            _lossSeed = default;
            _gain = default;
            _loss = default;
            _poisoned = false;
        }

        internal double? Next(double? price, bool publish = true)
        {
            if (_poisoned)
                return null;
            var ready = _history.Count == lag;
            var prior = ready ? _history.Dequeue() : null;
            _history.Enqueue(price);
            if (!ready)
                return null;
            var hasDifference = price.HasValue && prior.HasValue;
            if (_seedSteps < period)
            {
                if (hasDifference)
                {
                    if (price!.Value > prior!.Value)
                    {
                        _gainSeed.Add(price.Value); _gainSeed.Add(prior.Value, -1);
                    }
                    else if (price.Value < prior.Value)
                    {
                        _lossSeed.Add(prior.Value); _lossSeed.Add(price.Value, -1);
                    }
                    _known++;
                }
                if (++_seedSteps < period)
                    return null;
                if (_known == 0)
                {
                    _poisoned = true;
                    return null;
                }
                _gain = PositiveStrengthValue.Seed(_gainSeed, _known);
                _loss = PositiveStrengthValue.Seed(_lossSeed, _known);
                _gainSeed = default;
                _lossSeed = default;
            }
            else
            {
                if (!hasDifference)
                {
                    _poisoned = true;
                    return null;
                }
                if (PositiveStrengthValue.TryDifference(price!.Value, prior!.Value, out var compactChange))
                {
                    _gain = _gain.Update(Math.Max(compactChange, 0), period, IsMomentum(convention));
                    _loss = _loss.Update(Math.Max(-compactChange, 0), period, IsMomentum(convention));
                }
                else
                {
                    var difference = ExactVarianceWindow.Units(price!.Value) - ExactVarianceWindow.Units(prior!.Value);
                    var up = BigInteger.Max(difference, 0);
                    var down = BigInteger.Max(-difference, 0);
                    _gain = IsMomentum(convention)
                        ? _gain.UpdateExponential(up, period)
                        : _gain.Update(up, period);
                    _loss = IsMomentum(convention)
                        ? _loss.UpdateExponential(down, period)
                        : _loss.Update(down, period);
                }
            }
            // Historical replay needs the recurrence and poisoning behavior,
            // but only the requested bar needs a rounded output ratio.
            if (
                !publish || _loss.IsZero
                || convention == NullableStrengthConvention.RelativeMomentumIndex && _gain.IsZero
            )
                return null;
            return convention switch
            {
                NullableStrengthConvention.RelativeStrength
                or NullableStrengthConvention.RelativeMomentum => PositiveStrengthValue.Quotient(
                    _gain,
                    _loss
                ),
                _ => PositiveStrengthValue.Percent(
                    _gain,
                    _loss,
                    convention == NullableStrengthConvention.NetMomentum
                ),
            };
        }
    }

    private sealed class State(int period, NullableStrengthConvention convention, int lag)
        : IMultiOutputState
    {
        private readonly Calculator _calculator = new(period, convention, lag);

        public void Reset() => _calculator.Reset();

        public void Update(in Bar bar, Span<double> output)
        {
            var value = _calculator.Next(bar.Close);
            output[0] = value ?? 0;
            output[1] = value.HasValue ? 1 : 0;
        }
    }
}
