#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeMoneyFlowColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, WindowMoneyFlowIndex indicator, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        int count = seed.Length + end - start;
        var captured = System.Buffers.ArrayPool<Bar>.Shared.Rent(count);
        try
        {
            int priceGrid = int.MaxValue, volumeGrid = int.MaxValue;
            double maximumPrice = 0, maximumVolume = 0;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = start + i - seed.Length;
                var bar = i < seed.Length ? seed[i]
                    : row >= end - tail.Length ? tail[row - (end - tail.Length)] : source[row];
                if (!AllFieldsFinite(in bar)) { region.InvalidInput = true; region.Last = bar; return region; }
                if (i >= seed.Length)
                {
                    region.Last = bar;
                    if (owned is not null) owned[row] = bar;
                }
                double typical = ExactTypicalPrice.Calculate(bar.High, bar.Low, bar.Close);
                // Open is unused by MFI. Keep original HLCV for the exact fallback,
                // and cache its once-rounded typical price in this owned slot.
                captured[i] = new Bar(bar.Time, typical, bar.High, bar.Low, bar.Close, bar.Volume);
                IncludeIntegerGrid(typical, ref priceGrid, ref maximumPrice);
                IncludeIntegerGrid(bar.Volume, ref volumeGrid, ref maximumVolume);
            }
            if (priceGrid == int.MaxValue) priceGrid = 0;
            if (volumeGrid == int.MaxValue) volumeGrid = 0;
            double priceBound = Math.ScaleB(maximumPrice, -priceGrid);
            double volumeBound = Math.ScaleB(maximumVolume, -volumeGrid);
            // Power-of-two scaling preserves the source significand exactly;
            // larger integers remain exact when their low bits are zero.
            const double longExclusiveLimit = 9223372036854775808d;
            bool certified = priceBound < longExclusiveLimit && volumeBound < longExclusiveLimit;
            if (certified)
            {
                UInt128 product = (UInt128)(ulong)priceBound * (ulong)volumeBound;
                // At most P directional flows in total; preserve headroom for
                // the exact numerator's factor 100, including signed volumes.
                certified = product <= (UInt128)Int128.MaxValue / 100 / (uint)indicator.Period;
            }
            if (!certified)
            {
                var state = new WindowMoneyFlowIndex.CompactState(indicator.Period, indicator.MinimumTotalOne,
                    Math.Max(0, indicator.SuppressedBars - (start - seed.Length)), count);
                Span<double> values = stackalloc double[2];
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    state.Update(in captured[i], values);
                    if (i < seed.Length) continue;
                    int row = start + i - seed.Length;
                    output[0][row] = values[0]; output[1][row] = values[1];
                    RecordMoneyFlowOutput(ref region, row, values[0]);
                }
                return region;
            }
            // A representable power-of-two multiplier preserves these certified
            // integers exactly. Subnormal grids can require an infinite inverse;
            // retain ScaleB for those grids instead of multiplying by infinity.
            double priceFactor = priceGrid >= -1023 ? Math.ScaleB(1d, -priceGrid) : 0;
            double volumeFactor = volumeGrid >= -1023 ? Math.ScaleB(1d, -volumeGrid) : 0;
            // Reuse the capture as compact contributions after certification.
            // Each 32-byte pair ends before the next unread 48-byte Bar. Read
            // the current bar completely before writing its pair; owned history
            // and fallback decisions have already been handled above.
            var flows = System.Runtime.InteropServices.MemoryMarshal.Cast<Bar, MoneyFlowIntegerPair>(captured.AsSpan(0, count));
            Int128 positive = 0, negative = 0;
            long previousPrice = 0;
            int scale = priceGrid + volumeGrid;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                var bar = captured[i];
                long price = (long)(priceFactor == 0 ? Math.ScaleB(bar.Open, -priceGrid) : bar.Open * priceFactor);
                long volume = (long)(volumeFactor == 0 ? Math.ScaleB(bar.Volume, -volumeGrid) : bar.Volume * volumeFactor);
                MoneyFlowIntegerPair current = default;
                if (i > 0)
                {
                    Int128 flow = unchecked((Int128)price * volume);
                    if (price > previousPrice) current.Positive = flow;
                    else if (price < previousPrice) current.Negative = flow;
                }
                previousPrice = price;
                flows[i] = current;
                // The certificate bounds every product and P-term sum with
                // 100-fold publication headroom, including signed volumes.
                unchecked
                {
                    if (i > indicator.Period)
                    {
                        var expired = flows[i - indicator.Period];
                        positive -= expired.Positive; negative -= expired.Negative;
                    }
                    positive += current.Positive; negative += current.Negative;
                }
                if (i < seed.Length) continue;
                int row = start + i - seed.Length;
                bool defined = i >= indicator.Period && row >= (long)indicator.Period + indicator.SuppressedBars;
                double value = 0;
                if (defined)
                {
                    Int128 total = unchecked(positive + negative);
                    bool belowOne = total <= 0 || scale < 0 && (-scale >= 127 || total < (Int128.One << -scale));
                    if (indicator.MinimumTotalOne && belowOne) value = 0;
                    else if (!indicator.MinimumTotalOne && negative == 0) value = 100;
                    else if (total == 0) value = double.NegativeInfinity;
                    else if (positive != 0) value = JurikDyadic.IntegerRatio(unchecked(100 * positive), total);
                }
                output[0][row] = value; output[1][row] = defined ? 1 : 0;
                RecordMoneyFlowOutput(ref region, row, value);
            }
            return region;
        }
        finally { System.Buffers.ArrayPool<Bar>.Shared.Return(captured); }
    }

    private struct MoneyFlowIntegerPair
    {
        internal Int128 Positive;
        internal Int128 Negative;
    }

    private static void RecordMoneyFlowOutput(ref PointwiseRegion region, int index, double value)
    {
        if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
        { region.InvalidOutputIndex = index; region.InvalidOutputValue = value; }
    }
}
#endif
