#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class SmoothedRegion
    {
        internal SmoothedAccumulationOscillator.CompactMean? Fast, Slow;
        internal SmoothedAccumulationOscillator.MeanContinuation StartFast, StartSlow;
        internal bool Ready;
        internal Exception? Error;
    }

    private static bool TryFillSmoothedRegions(Bar[] source, Node node,
        SmoothedAccumulationOscillator owner, CancellationToken cancellation,
        OwnedBarBuffer? owned, out Bar latest)
    {
        latest = default;
        if (!CanParallelize(source.Length, 8192)) return false;
        int chunks = WorkerCount();
        long replayLength = 32L * Math.Max(owner.FastPeriod, owner.SlowPeriod);
        if (chunks < 2 || replayLength > source.Length / chunks || !Monitor.TryEnter(ParallelBarGate)) return false;
        try
        {
            int count = source.Length;
            var captured = owned ?? new OwnedBarBuffer(count);
            for (int i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                latest = source[i];
                if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                captured[i] = latest;
            }

            // Disabled detail columns alias zero storage. The oscillator is
            // written concurrently, so neither can hold the shared replay input.
            double[] line = owner.IncludeDetails ? node.Values[3] : new double[count];
            var summary = new Core.SmaCpuKernel.GridSummary();
            for (int i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!owner.TryBatchFlow(in captured[i], out double multiplier, out double flow))
                {
                    FillOriginalSmoothed(captured, node, owner, cancellation);
                    return true;
                }
                line[i] = flow;
                summary.Include(flow);
                if (owner.IncludeDetails)
                {
                    // Original dyadic publication canonicalizes rounded zero.
                    node.Values[1][i] = multiplier == 0 ? 0 : multiplier; // NOSONAR: Exact zero contract.
                    node.Values[2][i] = flow == 0 ? 0 : flow; // NOSONAR: Exact zero contract.
                    node.Values[5][i] = 1;
                }
            }
            if (!summary.CertifiesCompensatedPrefix(count))
            {
                FillOriginalSmoothed(captured, node, owner, cancellation);
                return true;
            }
            double high = 0, low = 0;
            for (int i = 0; i < count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                (high, low) = CompensatedSum.Add(high, low, line[i]);
                line[i] = high == 0 ? 0 : high; // NOSONAR: Preserve canonical exact zero.
            }

            var regions = new SmoothedRegion[chunks];
            int replay = (int)replayLength;
            AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                var region = new SmoothedRegion();
                regions[chunk] = region;
                try
                {
                    var fast = new SmoothedAccumulationOscillator.CompactMean(owner.FastPeriod, owner.FirstValue);
                    var slow = new SmoothedAccumulationOscillator.CompactMean(owner.SlowPeriod, owner.FirstValue);
                    region.Fast = fast;
                    region.Slow = slow;
                    int start = (int)((long)count * chunk / chunks);
                    int end = (int)((long)count * (chunk + 1) / chunks);
                    for (int i = Math.Max(0, start - replay); i < start; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        AddSmoothedLine(fast, slow, line[i]);
                    }
                    region.StartFast = fast.CaptureContinuation();
                    region.StartSlow = slow.CaptureContinuation();
                    region.Ready = start == 0 || region.StartFast.Ready && region.StartSlow.Ready;
                    if (!region.Ready) return;
                    for (int i = start; i < end; i++)
                    {
                        if (cancellation.IsCancellationRequested) return;
                        AddSmoothedLine(fast, slow, line[i]);
                        WriteSmoothedValue(owner, fast, slow, i, node.Values);
                    }
                }
                catch (Exception error) { region.Error = error; }
            });
            cancellation.ThrowIfCancellationRequested();

            SmoothedAccumulationOscillator.CompactMean? actualFast = null, actualSlow = null;
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                cancellation.ThrowIfCancellationRequested();
                var region = regions[chunk];
                // Replay is only a heuristic. Both means must match the actual
                // preceding accepted/repaired state before speculative output survives.
                if (region.Ready && region.Error is null && (chunk == 0
                    || region.StartFast == actualFast!.CaptureContinuation()
                    && region.StartSlow == actualSlow!.CaptureContinuation()))
                {
                    actualFast = region.Fast;
                    actualSlow = region.Slow;
                    continue;
                }
                actualFast ??= new(owner.FastPeriod, owner.FirstValue);
                actualSlow ??= new(owner.SlowPeriod, owner.FirstValue);
                int start = (int)((long)count * chunk / chunks);
                int end = (int)((long)count * (chunk + 1) / chunks);
                for (int i = start; i < end; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        AddSmoothedLine(actualFast, actualSlow, line[i]);
                        WriteSmoothedValue(owner, actualFast, actualSlow, i, node.Values);
                    }
                    catch (Exception error) { node.CaptureFailure(error); return true; }
                }
            }
            return true;
        }
        finally { Monitor.Exit(ParallelBarGate); }
    }

    private static void AddSmoothedLine(SmoothedAccumulationOscillator.CompactMean fast,
        SmoothedAccumulationOscillator.CompactMean slow, double line)
    {
        var value = JurikDyadic.FromDouble(line);
        fast.Add(value);
        slow.Add(value);
    }

    private static void WriteSmoothedValue(SmoothedAccumulationOscillator owner,
        SmoothedAccumulationOscillator.CompactMean fast, SmoothedAccumulationOscillator.CompactMean slow,
        int index, double[][] output)
    {
        if (index < (long)Math.Max(owner.FastPeriod, owner.SlowPeriod) - 1 + owner.Suppression)
        {
            output[0][index] = output[4][index] = 0;
            return;
        }
        var oscillator = fast.Value;
        oscillator.Subtract(slow.Value);
        oscillator = oscillator.Rounded();
        output[0][index] = oscillator.Sign == 0 ? 0 : oscillator.Mean(1);
        output[4][index] = 1;
    }

    private static void FillOriginalSmoothed(OwnedBarBuffer captured, Node node,
        SmoothedAccumulationOscillator owner, CancellationToken cancellation)
    {
        var state = (IMultiOutputState)owner.CreateState();
        Span<double> scratch = stackalloc double[8];
        for (int i = 0; i < node.Values[0].Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                state.Update(in captured[i], scratch);
                for (int slot = 0; slot < node.Values.Length; slot++) node.Values[slot][i] = scratch[slot];
            }
            catch (Exception error) { node.CaptureFailure(error); return; }
        }
    }
}
#endif
