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
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                var bar = captured[i];
                captured[i] = new Bar(default, Math.ScaleB(bar.Open, -priceGrid), 0, 0, 0,
                    Math.ScaleB(bar.Volume, -volumeGrid));
            }
            Int128 positive = 0, negative = 0;
            int scale = priceGrid + volumeGrid;
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                if (i > indicator.Period)
                {
                    int old = i - indicator.Period;
                    AccumulateIntegerFlow(in captured[old], in captured[old - 1], -1, ref positive, ref negative);
                }
                if (i > 0) AccumulateIntegerFlow(in captured[i], in captured[i - 1], 1, ref positive, ref negative);
                if (i < seed.Length) continue;
                int row = start + i - seed.Length;
                bool defined = i >= indicator.Period && row >= (long)indicator.Period + indicator.SuppressedBars;
                double value = 0;
                if (defined)
                {
                    Int128 total = positive + negative;
                    bool belowOne = total <= 0 || scale < 0 && (-scale >= 127 || total < (Int128.One << -scale));
                    if (indicator.MinimumTotalOne && belowOne) value = 0;
                    else if (!indicator.MinimumTotalOne && negative == 0) value = 100;
                    else if (total == 0) value = double.NegativeInfinity;
                    else if (positive != 0) value = JurikDyadic.FromInteger(100 * positive).Ratio(JurikDyadic.FromInteger(total));
                }
                output[0][row] = value; output[1][row] = defined ? 1 : 0;
                RecordMoneyFlowOutput(ref region, row, value);
            }
            return region;
        }
        finally { System.Buffers.ArrayPool<Bar>.Shared.Return(captured); }
    }

    private static void AccumulateIntegerFlow(in Bar current, in Bar previous, int sign, ref Int128 positive, ref Int128 negative)
    {
        Int128 flow = (Int128)(long)current.Open * (long)current.Volume * sign;
        if (current.Open > previous.Open) positive += flow;
        else if (current.Open < previous.Open) negative += flow;
    }

    private static void RecordMoneyFlowOutput(ref PointwiseRegion region, int index, double value)
    {
        if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
        { region.InvalidOutputIndex = index; region.InvalidOutputValue = value; }
    }
}
#endif
