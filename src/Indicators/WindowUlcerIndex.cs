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
        private struct Entry(double price)
        {
            internal double Price = price;
            internal double Peak = -1; // No cached contribution yet; actual peaks are nonnegative.
            internal RocBankValue Drawdown;
        }

        private readonly List<Entry> _prices = new();
        private int _start;
        private JurikDyadic _squares;

        public void Reset()
        {
            _prices.Clear(); _start = 0; _squares = default;
        }

        private void AccumulateSquare(RocBankValue drawdown, bool add)
        {
            if (drawdown.Mantissa == 0) return;
            var square = JurikDyadic.FromDouble(drawdown.Mantissa);
            square.ScaleByPowerOfTwo(drawdown.UpperShift);
            square.Square();
            if (add) _squares.AddExact(square); else _squares.Subtract(square);
        }

        private static RocBankValue Drawdown(double price, double peak)
        {
            if (peak == 0 || price == peak) return default;
            var numerator = JurikDyadic.FromDouble(price);
            numerator.Subtract(JurikDyadic.FromDouble(peak));
            numerator.Multiply(100);
            double ratio = numerator.Ratio(JurikDyadic.FromDouble(peak));
            // Preserve extended unpublished percentages even when final RMS is finite.
            return double.IsInfinity(ratio) ? RocBankValue.Return(price, peak) : new RocBankValue(ratio);
        }

        public void Update(in Bar bar, Span<double> output)
        {
            output.Clear();
            double value = bar.Close;
            if (!FrameworkCompatibility.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_prices.Count == period)
            {
                AccumulateSquare(_prices[_start].Drawdown, false);
                _prices[_start] = new Entry(value);
                if (++_start == period) _start = 0;
            }
            else _prices.Add(new Entry(value));
            if (_prices.Count < period) return;

            double peak = 0;
            bool defined = true;
            int index = _start;
            for (int offset = 0; offset < period; offset++)
            {
                var entry = _prices[index];
                peak = Math.Max(peak, entry.Price);
                if (peak == 0) defined = false;
                if (entry.Peak != peak)
                {
                    // Changes to the left boundary can lower the prefix peak.
                    // Remove the old exact contribution before installing its replacement.
                    AccumulateSquare(entry.Drawdown, false);
                    entry.Peak = peak;
                    entry.Drawdown = Drawdown(entry.Price, peak);
                    AccumulateSquare(entry.Drawdown, true);
                    _prices[index] = entry;
                }
                if (++index == period) index = 0;
            }
            // Even absent windows maintain their defined suffix contributions,
            // so expiration of a nonpositive prefix recovers without stale squares.
            if (!defined) return;
            output[0] = _squares.SqrtMean(period);
            output[1] = 1;
        }
    }
}
