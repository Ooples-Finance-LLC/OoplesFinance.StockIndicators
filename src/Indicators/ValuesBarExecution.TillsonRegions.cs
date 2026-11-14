#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class TillsonRegion
    {
        internal TillsonValueState? State;
        internal TillsonContinuation Start;
        internal bool Ready;
        internal Exception? Error;
    }

    private static TillsonValueState CreateTillsonRegionState(TillsonAverage owner) =>
        new(owner.Period, owner.VolumeFactor, owner.Seed, owner.Suppression);

    private static bool TryFillTillsonRegions(Bar[] source, Node node, TillsonAverage owner,
        CancellationToken cancellation, OwnedBarBuffer? owned, out Bar latest)
    {
        latest = default;
        if (owner.Seed != TillsonSeed.CascadedMeans || !CanParallelize(source.Length, 8192)) return false;
        int chunks = WorkerCount();
        long replayLength = 32L * owner.Period;
        if (chunks < 2 || replayLength > source.Length / chunks || !Monitor.TryEnter(ParallelBarGate)) return false;
        try
        {
            // Freeze each caller bar once before workers replay overlapping
            // regions. Raw-input errors still precede every state/output error.
            cancellation.ThrowIfCancellationRequested();
            int count = source.Length;
            // Full history already owns this fixed storage. LatestOnly needs
            // private capture storage, but neither mode rereads caller bars.
            var captured = owned ?? new OwnedBarBuffer(count);
            for (int i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                latest = source[i];
                if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                captured[i] = latest;
            }
            cancellation.ThrowIfCancellationRequested();
            var regions = new TillsonRegion[chunks];
            int replay = (int)replayLength;
            AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                var region = new TillsonRegion();
                regions[chunk] = region;
                try
                {
                    var state = CreateTillsonRegionState(owner);
                    region.State = state;
                    int start = (int)((long)count * chunk / chunks);
                    int end = (int)((long)count * (chunk + 1) / chunks);
                    Span<double> scratch = stackalloc double[2];
                    for (int i = Math.Max(0, start - replay); i < start; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        state.Update(in captured[i], scratch);
                    }
                    // Replay length is only a scheduling heuristic. The index
                    // can move only after completed seeds, never during startup.
                    region.Ready = start == 0 || state.TrySetContinuationIndex(start - 1L);
                    if (!region.Ready) return;
                    region.Start = state.CaptureContinuation();
                    for (int i = start; i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        state.Update(in captured[i], scratch);
                        node.Values[0][i] = scratch[0];
                        node.Values[1][i] = scratch[1];
                    }
                }
                catch (Exception error) { region.Error = error; }
            });
            cancellation.ThrowIfCancellationRequested();

            TillsonValueState? actual = null;
            Span<double> repaired = stackalloc double[2];
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                cancellation.ThrowIfCancellationRequested();
                var region = regions[chunk];
                // The first region used the original seed. Every later region
                // must agree with the actual preceding end state, not merely
                // with another independently restarted window.
                if (region.Ready && region.Error is null && (chunk == 0
                    || region.Start.IsSeeded && actual!.CaptureContinuation() == region.Start))
                {
                    actual = region.State;
                    continue;
                }
                // Speculative errors and mismatches are not published. Repair
                // from the actual state with the unchanged original update.
                actual ??= CreateTillsonRegionState(owner);
                int start = (int)((long)count * chunk / chunks);
                int end = (int)((long)count * (chunk + 1) / chunks);
                for (int i = start; i < end; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        actual.Update(in captured[i], repaired);
                        node.Values[0][i] = repaired[0];
                        node.Values[1][i] = repaired[1];
                    }
                    catch (Exception error)
                    {
                        node.CaptureFailure(error);
                        return true;
                    }
                }
            }
            return true;
        }
        finally { Monitor.Exit(ParallelBarGate); }
    }
}
#endif
