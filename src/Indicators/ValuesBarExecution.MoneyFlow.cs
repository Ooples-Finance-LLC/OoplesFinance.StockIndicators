#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using System.Numerics;

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
            bool certified = Core.MoneyFlowIntegerCertificate.TryGetProduct(priceBound, volumeBound, out var product)
                && Core.MoneyFlowIntegerCertificate.FitsInt128(product, indicator.Period);
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
            return Core.MoneyFlowIntegerCertificate.FitsInt64(product, indicator.Period)
                ? ComputeIntegerMoneyFlow<long>(captured.AsSpan(0, count), output, start, indicator, seed.Length,
                    priceGrid, volumeGrid, region, cancellation)
                : ComputeIntegerMoneyFlow<Int128>(captured.AsSpan(0, count), output, start, indicator, seed.Length,
                    priceGrid, volumeGrid, region, cancellation);
        }
        finally { System.Buffers.ArrayPool<Bar>.Shared.Return(captured); }
    }

    // Value-type specialization keeps one recurrence for both certified widths.
    private static PointwiseRegion ComputeIntegerMoneyFlow<T>(Span<Bar> captured, double[][] output, int start,
        WindowMoneyFlowIndex indicator, int seedLength, int priceGrid, int volumeGrid,
        PointwiseRegion region, CancellationToken cancellation) where T : unmanaged, IBinaryInteger<T>
    {
        int count = captured.Length;
        // A representable power-of-two multiplier preserves these certified
        // integers exactly. Subnormal grids can require an infinite inverse;
        // retain ScaleB for those grids instead of multiplying by infinity.
        double priceFactor = priceGrid >= -1023 ? Math.ScaleB(1d, -priceGrid) : 0;
        double volumeFactor = volumeGrid >= -1023 ? Math.ScaleB(1d, -volumeGrid) : 0;
        // Reuse the capture as compact contributions after certification.
        // Each 16- or 32-byte pair ends before the next unread 48-byte Bar. Read
        // the current bar completely before writing its pair; owned history
        // and fallback decisions have already been handled above.
        var flows = System.Runtime.InteropServices.MemoryMarshal.Cast<Bar, MoneyFlowIntegerPair<T>>(captured);
        T positive = T.Zero, negative = T.Zero;
        long previousPrice = 0;
        int scale = priceGrid + volumeGrid;
        for (int i = 0; i < count; i++)
        {
            if (cancellation.IsCancellationRequested) return region;
            var bar = captured[i];
            long price = (long)(priceFactor == 0 ? Math.ScaleB(bar.Open, -priceGrid) : bar.Open * priceFactor);
            long volume = (long)(volumeFactor == 0 ? Math.ScaleB(bar.Volume, -volumeGrid) : bar.Volume * volumeFactor);
            MoneyFlowIntegerPair<T> current = default;
            if (i > 0)
            {
                T flow = unchecked(T.CreateChecked(price) * T.CreateChecked(volume));
                if (price > previousPrice) current.Positive = flow;
                else if (price < previousPrice) current.Negative = flow;
            }
            previousPrice = price;
            flows[i] = current;
            // The certificate bounds each product and P-term sum in T.
            // Publication promotes the result before applying factor 100.
            unchecked
            {
                if (i > indicator.Period)
                {
                    var expired = flows[i - indicator.Period];
                    positive -= expired.Positive; negative -= expired.Negative;
                }
                positive += current.Positive; negative += current.Negative;
            }
            if (i < seedLength) continue;
            int row = start + i - seedLength;
            bool defined = i >= indicator.Period && row >= (long)indicator.Period + indicator.SuppressedBars;
            double value = 0;
            if (defined)
            {
                Int128 total = Int128.CreateChecked(unchecked(positive + negative));
                bool belowOne = total <= 0 || scale < 0 && (-scale >= 127 || total < (Int128.One << -scale));
                if (indicator.MinimumTotalOne && belowOne) value = 0;
                else if (!indicator.MinimumTotalOne && negative == T.Zero) value = 100;
                else if (total == 0) value = double.NegativeInfinity;
                else if (positive != T.Zero) value = JurikDyadic.IntegerRatio(unchecked(100 * Int128.CreateChecked(positive)), total);
            }
            output[0][row] = value; output[1][row] = defined ? 1 : 0;
            RecordMoneyFlowOutput(ref region, row, value);
        }
        return region;
    }

    private struct MoneyFlowIntegerPair<T> where T : unmanaged
    {
        internal T Positive;
        internal T Negative;
    }

    private static void RecordMoneyFlowOutput(ref PointwiseRegion region, int index, double value)
    {
        if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
        { region.InvalidOutputIndex = index; region.InvalidOutputValue = value; }
    }
}
#endif
