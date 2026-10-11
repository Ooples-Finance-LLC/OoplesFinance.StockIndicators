#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillTrueRange(Bar[] source, double[] output, IIndicator indicator,
        CancellationToken cancellation, Bar[]? owned)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!CanParallelize(source.Length, 8192) || !Monitor.TryEnter(ParallelBarGate))
        {
            var region = ComputeTrueRangeRegion(source, output, owned, 0, source.Length, default, default, false, cancellation);
            cancellation.ThrowIfCancellationRequested();
            ValidatePointwiseInput(in region);
            ValidatePointwiseOutput(indicator, in region);
            return region.Last;
        }
        try
        {
            int chunks = WorkerCount();
            var boundaries = new Bar[chunks];
            var regions = new PointwiseRegion[chunks];
            // Capture each region's final bar once. It is both that region's last
            // input and the next region's seed; workers never reread it from source.
            for (int chunk = 0; chunk < chunks; chunk++)
                boundaries[chunk] = source[(int)((long)source.Length * (chunk + 1) / chunks) - 1];
            AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = (int)((long)source.Length * chunk / chunks);
                int end = (int)((long)source.Length * (chunk + 1) / chunks);
                regions[chunk] = ComputeTrueRangeRegion(source, output, owned, start, end,
                    chunk == 0 ? default : boundaries[chunk - 1], boundaries[chunk], true, cancellation);
            });
            cancellation.ThrowIfCancellationRequested();
            foreach (var region in regions) ValidatePointwiseInput(in region);
            foreach (var region in regions) ValidatePointwiseOutput(indicator, in region);
            return regions[^1].Last;
        }
        finally { Monitor.Exit(ParallelBarGate); }
    }

    private static PointwiseRegion ComputeTrueRangeRegion(Bar[] source, double[] output, Bar[]? owned,
        int start, int end, Bar previous, Bar boundary, bool capturedBoundary, CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        Bar latest = default;
        double previousClose = previous.Close;
        for (int i = start; i < end; i++)
        {
            if (cancellation.IsCancellationRequested) break;
            latest = capturedBoundary && i == end - 1 ? boundary : source[i];
            if (!AllFieldsFinite(in latest)) { region.InvalidInput = true; break; }
            double value = i == 0 ? latest.High - latest.Low
                : Math.Max(latest.High - latest.Low,
                    Math.Max(Math.Abs(latest.High - previousClose), Math.Abs(latest.Low - previousClose)));
            previousClose = latest.Close;
            output[i] = value;
            if (owned is not null) owned[i] = latest;
            if (!double.IsFinite(value) && region.InvalidOutputIndex < 0)
            {
                region.InvalidOutputIndex = i;
                region.InvalidOutputValue = value;
            }
        }
        region.Last = latest;
        return region;
    }
}
#endif
