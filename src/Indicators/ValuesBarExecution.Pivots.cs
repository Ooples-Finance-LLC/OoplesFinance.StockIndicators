#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
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
