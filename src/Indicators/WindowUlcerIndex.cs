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

    internal sealed class State(int period) : IMultiOutputState
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

        private void ReplaceContribution(ref Entry entry, double peak)
        {
            AccumulateSquare(entry.Drawdown, false);
            entry.Peak = peak;
            entry.Drawdown = Drawdown(entry.Price, peak);
            AccumulateSquare(entry.Drawdown, true);
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
            int index = _start;
            int last = _start == 0 ? period - 1 : _start - 1;
#if !NETFRAMEWORK
            // All ring growth is complete for this update. Borrow entries only
            // until the scan ends; no helper can resize or retain this span.
            var entries = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_prices);
#endif
            // Repair only the surviving prefix affected by expiration. Once an
            // old/new prefix peak agrees, every later surviving peak also agrees.
            for (int offset = 0; offset < period - 1; offset++)
            {
#if !NETFRAMEWORK
                ref var entry = ref entries[index];
#else
                var entry = _prices[index];
#endif
                peak = Math.Max(peak, entry.Price);
                if (entry.Peak == peak)
                {
                    int priorLast = last == 0 ? period - 1 : last - 1;
#if !NETFRAMEWORK
                    peak = entries[priorLast].Peak;
#else
                    peak = _prices[priorLast].Peak;
#endif
                    break;
                }
                ReplaceContribution(ref entry, peak);
#if NETFRAMEWORK
                _prices[index] = entry;
#endif
                if (++index == period) index = 0;
            }
#if !NETFRAMEWORK
            ref var latest = ref entries[last];
#else
            var latest = _prices[last];
#endif
            // The appended entry is always new, even when the surviving suffix
            // was skipped. On the initial full window no sentinel peak can match.
            ReplaceContribution(ref latest, Math.Max(peak, latest.Price));
#if NETFRAMEWORK
            _prices[last] = latest;
#endif
            // A positive first price guarantees positive peaks in every prefix.
            // Absent windows still repair their suffix for subsequent recovery.
            if (_prices[_start].Price <= 0) return;
            output[0] = _squares.SqrtMean(period);
            output[1] = 1;
        }
    }
}
