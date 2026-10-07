using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Absolute or percentage difference between two classical moving averages.</summary>
/// <remarks>Periods are ordered so the smaller is fast. Each average uses its own complete
/// history and startup. The oscillator starts when both are present. Absolute difference
/// and percentage normalization round once over rounded averages with extended upper exponents, avoiding intermediate
/// subtraction overflow. A zero slow average returns zero in percentage mode. Each component
/// retains its rounding and startup contract, with wide extrapolation until final publication.</remarks>
public sealed class ClassicPriceOscillator : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates positive periods, any classical method and optional percentage normalization.</summary>
    public ClassicPriceOscillator(
        int fastPeriod = 12,
        int slowPeriod = 26,
        ClassicAverageMethod method = ClassicAverageMethod.Sma,
        bool percentage = false,
        bool firstPriceSeed = false,
        int suppression = 0
    )
        : base(2)
    {
        if (fastPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(fastPeriod));
        if (slowPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(slowPeriod));
        FastPeriod = Math.Min(fastPeriod, slowPeriod);
        SlowPeriod = Math.Max(fastPeriod, slowPeriod);
        Method = method;
        Percentage = percentage;
        FirstPriceSeed = firstPriceSeed;
        Suppression = suppression;
        Fast = new(FastPeriod, method, firstPriceSeed, suppression);
        Slow = new(SlowPeriod, method, firstPriceSeed, suppression);
    }

    /// <summary>Smaller averaging period.</summary>
    public int FastPeriod { get; }

    /// <summary>Larger averaging period.</summary>
    public int SlowPeriod { get; }

    /// <summary>Formula used by both averages.</summary>
    public ClassicAverageMethod Method { get; }

    /// <summary>Whether the difference is normalized by slow and multiplied by one hundred.</summary>
    public bool Percentage { get; }

    /// <summary>Exponential compatibility seed convention.</summary>
    public bool FirstPriceSeed { get; }

    /// <summary>Additional startup suppression for the selected averages.</summary>
    public int Suppression { get; }

    /// <summary>Oscillator, or zero before both averages are present.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when the oscillator is present.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];
    internal ClassicMovingAverage Fast { get; }
    internal ClassicMovingAverage Slow { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Math.Max(Fast.WarmupBars, Slow.WarmupBars);

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(this);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        Reference(bars)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private double?[] Reference(IReadOnlyList<Bar> bars)
    {
        var fast = ClassicAverageReference.ExtendedValues(bars, Fast);
        var slow = ClassicAverageReference.ExtendedValues(bars, Slow);
        var result = new double?[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            if (!fast[i].HasValue || !slow[i].HasValue)
                continue;
            var f = fast[i]!.Value;
            var s = slow[i]!.Value;
            result[i] = Percentage
                ? s.Sign == 0
                    ? 0
                    : ((f - s) * new ReferenceFraction(100) / s).ToDouble()
                : (f - s).ToDouble();
        }
        return result;
    }

    private sealed class State : IMultiOutputState, IDisposable
    {
        private readonly IMultiOutputState _fast,
            _slow;
        private readonly bool _percentage;
        private readonly double[] _f = new double[2],
            _s = new double[2];

        internal State(ClassicPriceOscillator owner)
        {
            _fast = (IMultiOutputState)owner.Fast.CreateState();
            _slow = (IMultiOutputState)owner.Slow.CreateState();
            _percentage = owner.Percentage;
        }

        public void Reset()
        {
            _fast.Reset();
            _slow.Reset();
            Array.Clear(_f, 0, 2);
            Array.Clear(_s, 0, 2);
        }

        public void Dispose()
        {
            if (_fast is IDisposable f)
                f.Dispose();
            if (_slow is IDisposable s)
                s.Dispose();
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            _fast.Update(bar, _f);
            _slow.Update(bar, _s);
            if (_f[1] == 0 || _s[1] == 0)
                return;
            var f = ((IRoundedAverageUnits)_fast).RoundedUnits;
            var s = ((IRoundedAverageUnits)_slow).RoundedUnits;
            if (_percentage)
            {
                var numerator = 100 * (f - s) << 1074;
                output[0] =
                    s.IsZero ? 0
                    : s.Sign < 0 ? ExactMeanAccumulator.UnitRatio(-numerator, -s)
                    : ExactMeanAccumulator.UnitRatio(numerator, s);
            }
            else
                output[0] = ExactMeanAccumulator.UnitRatio(f - s, 1);
            output[1] = 1;
        }
    }
}
