#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Each output depends on Period + Offset preceding bars and the current
    // open. Bound replay storage; larger windows keep the existing state path.
    private static int PivotReplayWindow(IIndicator indicator) => indicator is RollingPivotLevels pivot
        && (long)pivot.Period + pivot.Offset < 4096 ? pivot.Period + pivot.Offset + 1 : 0;

    private struct PivotWindowKernel(PivotValueState state, BoundedOutput target) : IBoundedKernel<PivotWindowKernel>
    {
        private int _index = target.Origin;
        public static PivotWindowKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new PivotValueState((RollingPivotLevels)indicator, count), target);
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[18];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }

    private sealed class PivotValueState(RollingPivotLevels owner, int count) : IMultiOutputState
    {
        // No preceding complete window exists when count does not exceed warmup.
        // Avoid reserving period/offset history that cannot publish a value.
        private readonly PivotCpuKernel? _kernel = (long)owner.Period + owner.Offset >= count ? null
            : new(owner.Period, owner.Offset, owner.Style, rejectOverflow: false);
        public void Reset() => _kernel?.Reset();
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_kernel is null) { outputs.Clear(); return; }
            _kernel.Update(in bar, outputs);
            for (int slot = 0; slot < 9; slot++)
            {
                bool present = !double.IsNaN(outputs[slot]);
                outputs[slot + 9] = present ? 1 : 0;
                if (!present) outputs[slot] = 0;
            }
        }
    }
}
#endif
