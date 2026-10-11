using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Root mean square percentage drawdown from each running peak within the current window.</summary>
/// <remarks>The peak restarts at zero at the beginning of every full window.
/// A window is absent if any prefix has no positive peak. Each percentage drawdown
/// rounds once at binary64 precision with an extended upper exponent; its square
/// and the sum stay exact until the final root rounds. Histories grow lazily.</remarks>
public sealed class WindowUlcerIndex : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a positive-period window-relative Ulcer Index.</summary>
    public WindowUlcerIndex(int period = 14)
        : base(2)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Full window length.</summary>
    public int Period { get; }

    /// <summary>Ulcer Index, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[0];

    /// <summary>One when a complete window has a positive peak in every prefix.</summary>
    public IIndicatorOutput IsDefined => Outputs[1];

    /// <inheritdoc/>
    protected internal override object CreateState() => new State(Period);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 2)
            .Select(slot =>
                IndicatorValidationRule.ReferenceWithOverflowRejection(
                    slot,
                    bars =>
                        WindowUlcerReference
                            .Calculate(bars, Period)
                            .Select(v =>
                                slot == 0 ? v ?? 0
                                : v.HasValue ? 1d
                                : 0
                            )
                            .ToArray(),
                    IndicatorErrorBudget.Exact
                )
            );

    private sealed class State(int period) : IMultiOutputState
    {
        private readonly Queue<double> _prices = new();

        public void Reset() => _prices.Clear();

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            if (_prices.Count == period)
                _prices.Dequeue();
            double value = bar.Close;
            if (!FrameworkCompatibility.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            _prices.Enqueue(value);
            if (_prices.Count < period)
                return;
            double peak = 0;
            var squares = new JurikDyadic();
            foreach (var price in _prices)
            {
                peak = Math.Max(peak, price);
                if (peak == 0)
                    return;
                var numerator = JurikDyadic.FromDouble(price);
                numerator.Subtract(JurikDyadic.FromDouble(peak));
                numerator.Multiply(100);
                double ratio = numerator.Ratio(JurikDyadic.FromDouble(peak));
                JurikDyadic drawdown;
                if (double.IsInfinity(ratio))
                {
                    // An unpublished percentage can exceed binary64 even when
                    // the final RMS is finite. Retain its rounded significand
                    // and extended exponent before squaring exactly.
                    var extended = RocBankValue.Return(price, peak);
                    drawdown = JurikDyadic.FromDouble(extended.Mantissa);
                    drawdown.ScaleByPowerOfTwo(extended.UpperShift);
                }
                else drawdown = JurikDyadic.FromDouble(ratio);
                drawdown.Square();
                squares.AddExact(drawdown);
            }
            output[0] = squares.SqrtMean(period);
            output[1] = 1;
        }
    }
}
