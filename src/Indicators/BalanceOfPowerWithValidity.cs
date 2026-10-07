using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Simple average of (close-open)/(high-low), with explicit missing-window validity.</summary>
/// <remarks>A zero-range candle makes its entire averaging window unavailable. Value is zero when
/// IsDefined is zero, including startup. Each ratio is rounded once, then their mean is rounded once.
/// Storage grows with observed history up to Period.</remarks>
public sealed class BalanceOfPowerWithValidity
    : MultiOutputIndicatorBase,
        IIndicatorValidationContract
{
    /// <summary>Creates a balance-of-power average with a positive smoothing period.</summary>
    public BalanceOfPowerWithValidity(int period = 14)
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
        (Value, IsDefined) = DeclaredOutputs;
    }

    /// <summary>Number of ratios in the simple average.</summary>
    public int Period { get; }

    /// <summary>Mean ratio, or a zero placeholder when unavailable.</summary>
    public IIndicatorOutput Value { get; }

    /// <summary>One for a complete window without zero-range candles; otherwise zero.</summary>
    public IIndicatorOutput IsDefined { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period - 1;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Reference(0, bars => Reference(bars, false), 0, 0),
            IndicatorValidationRule.Reference(1, bars => Reference(bars, true), 0, 0),
            IndicatorValidationRule.Bounds(0, -1, 1),
            IndicatorValidationRule.Bounds(1, 0, 1),
        ];

    private IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars, bool validity)
    {
        var result = new double[bars.Count];
        for (var i = Period - 1; i < bars.Count; i++)
        {
            var window = bars.Skip(i - Period + 1).Take(Period).ToArray();
#pragma warning disable S1244 // Only an exactly zero range is missing; nonzero subnormal ranges remain valid.
            if (window.Any(b => b.High == b.Low))
#pragma warning restore S1244
                continue;
            var sum = new ReferenceFraction(0);
            foreach (var b in window)
            {
                var ratio = (
                    (ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open))
                    / (ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low))
                ).ToDouble();
                sum += ReferenceFraction.FromDouble(ratio);
            }
            result[i] = validity ? 1 : (sum / new ReferenceFraction(Period)).ToDouble();
        }
        return result;
    }

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<(double Value, bool Missing)> _window = new();
        private ExactMeanAccumulator _sum;
        private int _missing;

        public void Reset()
        {
            _window.Clear();
            _sum = default;
            _missing = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
#pragma warning disable S1244 // Only an exactly zero range is missing; nonzero subnormal ranges remain valid.
            var missing = bar.High == bar.Low;
#pragma warning restore S1244
            var value = missing
                ? 0
                : RoundedBalanceOfPower.Of(bar.Open, bar.High, bar.Low, bar.Close);
            if (_window.Count == period)
            {
                var old = _window.Dequeue();
                _sum.Add(old.Value, -1);
                if (old.Missing)
                    _missing--;
            }
            _window.Enqueue((value, missing));
            _sum.Add(value);
            if (missing)
                _missing++;
            var defined = _window.Count == period && _missing == 0;
            output[0] = defined ? _sum.Mean(period) : 0;
            output[1] = defined ? 1 : 0;
        }
    }
}
