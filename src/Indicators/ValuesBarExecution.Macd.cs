#if !NETFRAMEWORK
using System.Buffers;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int ClassicMacdWindow(ClassicMacd owner)
    {
        static bool Finite(ClassicMovingAverage average) => average.Period == 1
            || average.Method is ClassicAverageMethod.Sma or ClassicAverageMethod.Wma or ClassicAverageMethod.Trima;
        long window = owner.OscillatorFirst + owner.SignalAverage.First + 1;
        return Finite(owner.FastAverage) && Finite(owner.SlowAverage) && Finite(owner.SignalAverage)
            && window <= 4096 ? (int)window : 0;
    }

    // Return an earlier canonical column only when the public columns are equal
    // for every observation, including warmup and unselected hidden overflow.
    private static int MacdColumnOwner(IIndicator indicator, int slot)
    {
        if (indicator is ClassicMacd) return slot >= 3 ? 3 : slot;
        int count = indicator is AlignedEmaMacd ? 3 : 5;
        int selection = indicator is AlignedEmaMacd aligned ? (int)aligned.Selection : (int)((EmaDifferenceSignal)indicator).Selection;
        bool selected = (selection & (1 << (slot % count))) != 0;
        if (selected && (indicator is not AlignedEmaMacd || slot < count)) return slot;
        int candidates = selected ? selection : ((1 << count) - 1) & ~selection;
        int first = 0;
        while ((candidates & (1 << first)) == 0) first++;
        return selected ? first + count : first;
    }

    private static Bar FillClassicMacd(Bar[] source, double[][] output, ClassicMacd owner,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int period = ClassicMacdWindow(owner);
        int chunks = Math.Min(WorkerCount(), source.Length / period);
        bool locked = false;
        double[] prices = ArrayPool<double>.Shared.Rent(source.Length);
        Bar latest = default;
        try
        {
            for (int i = 0; i < source.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                latest = source[i];
                if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                prices[i] = latest.Close;
                if (owned is not null) owned[i] = latest;
            }
            if (chunks >= 2 && CanParallelize(source.Length, 512)) locked = Monitor.TryEnter(ParallelBarGate);
            if (!locked) chunks = 1;
            void Fill(int chunk)
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                // Replay both aligned input averages and the complete signal window.
                // Every replay owns and disposes the original average engine.
                using var state = new ClassicMacd.State(owner);
                Span<double> values = stackalloc double[6];
                for (int i = Math.Max(0, start - period + 1); i < end; i++)
                {
                    if (cancellation.IsCancellationRequested) return;
                    var bar = new Bar(default, 0, 0, 0, prices[i], 0);
                    state.Update(in bar, values);
                    if (i >= start)
                        for (int slot = 0; slot < 6; slot++) output[slot][i] = values[slot];
                }
            }
            if (chunks == 1) Fill(0);
            else CpuParallelSettings.LightweightParallel(chunks, chunks, Fill);
            cancellation.ThrowIfCancellationRequested();
            return latest;
        }
        finally
        {
            if (locked) Monitor.Exit(ParallelBarGate);
            ArrayPool<double>.Shared.Return(prices);
        }
    }
}
#endif
