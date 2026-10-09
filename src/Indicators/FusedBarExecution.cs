using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Indicators;

// A per-build plan for the two pilot formulas, including Asin(SMA(close)).
// Only sealed nodes participate: customer state, domains, projections and other dependencies keep
// their normal execution and error ordering. No caller buffers are retained.
internal sealed class FusedBarExecution
{
    private readonly bool _asinOfSma;
    private readonly Dictionary<IIndicator, FusedBarExecution>? _regions;
    private readonly FusedBarExecution[]? _regionPlans;
    internal int SmaLength { get; }
    internal double[]? SmaValues { get; }
    internal double[][]? AsinValues { get; }
    internal bool UsedSmaFallback { get; private set; }

    private FusedBarExecution(int count, int smaLength, bool asin, bool asinOfSma, bool publishSma, Dictionary<IIndicator, FusedBarExecution>? regions = null)
    {
        _regions = regions;
        _regionPlans = regions?.Values.Distinct().ToArray();
        SmaLength = smaLength;
        _asinOfSma = asinOfSma;
        if (publishSma) SmaValues = AllocateOutput(count);
        if (asin) AsinValues = new[] { AllocateOutput(count), AllocateOutput(count) };
    }

    private static double[] AllocateOutput(int count)
    {
#if NETFRAMEWORK
        return new double[count];
#else
        // Every slot is initialized before publication, including warmup/domain zeros.
        return GC.AllocateUninitializedArray<double>(count);
#endif
    }

    internal static FusedBarExecution? TryCreate(IReadOnlyList<IIndicator> indicators, int count)
    {
        var single = TryCreateSingle(indicators, count);
        if (single is not null) return single;
#if NETFRAMEWORK
        return null;
#else
        var groups = new Dictionary<int, List<IIndicator>>();
        foreach (var indicator in indicators)
        {
            // Validate the same sealed-node boundary without allocating full outputs.
            if (TryCreateSingle(new[] { indicator }, 0) is null) return null;
            var average = indicator as Sma ?? indicator.Source as Sma;
            var period = average is null ? 0 : Math.Max(1, average.Length);
            if (!groups.TryGetValue(period, out var group)) groups.Add(period, group = new());
            group.Add(indicator);
        }
        if (groups.Count == 0) return null;
        var regions = new Dictionary<IIndicator, FusedBarExecution>(IndicatorIdentity.Comparer);
        foreach (var group in groups.Values)
        {
            var plan = TryCreateSingle(group, count)!;
            foreach (var indicator in group) regions[indicator] = plan;
        }
        return new FusedBarExecution(count, 0, false, false, false, regions);
#endif
    }

    private static FusedBarExecution? TryCreateSingle(IReadOnlyList<IIndicator> indicators, int count)
    {
#if NETFRAMEWORK
        // The modern certificate is not the Framework SMA arithmetic contract.
        return null;
#else
        var length = 0;
        var publishSma = false;
        bool? asinOfSma = null;
        foreach (var indicator in indicators)
        {
            if (indicator.Components.Count != 0) return null;
            Sma? average;
            if (indicator is Sma sma) { average = sma; publishSma = true; }
            else if (indicator is PriceCircularTransform { Operation: PriceCircularOperation.ArcSine })
            {
                if (indicator.Source is not (null or Sma)) return null;
                var composed = indicator.Source is Sma;
                // Different Asin inputs need separate outputs/plans.
                if (asinOfSma.HasValue && asinOfSma.Value != composed) return null;
                asinOfSma = composed;
                average = indicator.Source as Sma;
            }
            else return null;
            if (average is null) continue;
            if (average.Source is not null || average.Components.Count != 0) return null;
            var normalized = Math.Max(1, average.Length);
            if (length != 0 && length != normalized) return null;
            length = normalized;
        }
        return length != 0 || asinOfSma.HasValue
            ? new FusedBarExecution(count, length, asinOfSma.HasValue, asinOfSma == true, publishSma) : null;
#endif
    }

    internal double[][] Values(IIndicator indicator) => _regions is not null ? _regions[indicator].Values(indicator)
        : indicator is Sma ? new[] { SmaValues! } : AsinValues!;

    internal Builder.IndicatorExecutionInfo Execute(Bar[] source, OwnedBarHistory history,
        CancellationToken cancellation, Builder.IndicatorExecutionBackend backend)
    {
        cancellation.ThrowIfCancellationRequested();
#if !NETFRAMEWORK
        // Automatic GPU selection stays disabled until end-to-end evidence supports it.
        if (backend == Builder.IndicatorExecutionBackend.Gpu)
        {
            if (_regions is not null)
                throw new NotSupportedException("Required GPU execution currently supports one fused region per build.");
            if (source.Length == 0)
                throw new NotSupportedException("An empty run has no device work to execute.");
            if (SmaLength > 4096 && SmaLength <= source.Length)
                throw new NotSupportedException("The GPU pilot supports SMA windows up to 4096 bars.");
            if (!TensorsGpuExecution.TryGet(out var gpu, out var reason))
                throw new NotSupportedException(reason);
            gpu!.Execute(this, source, history, _asinOfSma, cancellation);
            return new(Builder.IndicatorExecutionBackend.Gpu, gpu.DeviceName,
                "Fused FP64 kernel executed through AiDotNet.Tensors OpenCL.");
        }
#endif
        if (backend == Builder.IndicatorExecutionBackend.Gpu)
            throw new NotSupportedException("GPU execution requires a modern .NET target.");
        Execute(source, history, cancellation);
        return new(Builder.IndicatorExecutionBackend.Cpu, null,
            backend == Builder.IndicatorExecutionBackend.Cpu ? "CPU execution requested." : "Automatic execution uses CPU pending a validated GPU crossover.");
    }

#if !NETFRAMEWORK
    internal bool OwnCloses(Bar[] source, OwnedBarHistory history, double[] close,
        CancellationToken cancellation)
    {
        var proof = new Core.SmaCpuKernel.Certificate(Math.Max(1, SmaLength));
        bool certified = true, allInDomain = true;
        var reader = new CloseReader(cancellation);
        // A single uninitialized allocation avoids promoting hundreds of small
        // history chunks during large fresh-builder runs. Ownership is unchanged.
        var contiguous = source.Length >= 16_384 ? GC.AllocateUninitializedArray<Bar>(source.Length) : null;
        for (var offset = 0; offset < source.Length;)
        {
            var size = Math.Min(1024, source.Length - offset);
            var chunk = contiguous is null ? GC.AllocateUninitializedArray<Bar>(size) : null;
            var owned = contiguous is null ? chunk.AsSpan() : contiguous.AsSpan(offset, size);
            source.AsSpan(offset, size).CopyTo(owned);
            for (var i = 0; i < size; i++)
            {
                var value = reader.Read(in owned[i]);
                close[offset + i] = value;
                if (SmaLength > 1 && SmaLength <= source.Length)
                    certified &= proof.Include(value);
                if (AsinValues is not null)
                {
                    var defined = value is >= -1 and <= 1;
                    allInDomain &= defined;
                    if (!_asinOfSma) AsinValues[1][offset + i] = defined ? 1 : 0;
                }
            }
            if (chunk is not null) history.AppendOwnedChunk(chunk);
            offset += size;
        }
        if (contiguous is not null) history.TakeOwnedArray(contiguous);
        if (!certified)
            throw new NotSupportedException("SMA inputs do not satisfy the exact GPU rolling-sum certificate.");
        // An average of in-domain inputs (and warmup zero) is in-domain too.
        // Direct Asin flags were already produced during the required validation pass.
        bool devicePresence = AsinValues is not null && _asinOfSma && !allInDomain
            && SmaLength <= source.Length;
        if (AsinValues is not null && _asinOfSma && !devicePresence)
            Array.Fill(AsinValues[1], 1d);
        return devicePresence;
    }

#endif

    internal void Execute(Bar[] source, OwnedBarHistory history, CancellationToken cancellation)
    {
#if NETFRAMEWORK
        throw new NotSupportedException("Fused pilot execution requires a modern runtime.");
#else
        if (_regionPlans is not null)
        {
            cancellation.ThrowIfCancellationRequested();
            var owned = GC.AllocateUninitializedArray<Bar>(source.Length);
            source.AsSpan().CopyTo(owned);
            var summary = new Core.SmaCpuKernel.GridSummary();
            var validator = new CloseReader(cancellation);
            foreach (ref readonly var bar in owned.AsSpan()) summary.Include(validator.Read(in bar));
            history.TakeOwnedArray(owned);
            UsedSmaFallback = false;
            foreach (var region in _regionPlans)
            {
                region.ExecutePrepared(owned, summary, cancellation);
                UsedSmaFallback |= region.UsedSmaFallback;
            }
            return;
        }
        if (SmaLength > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var owned = GC.AllocateUninitializedArray<Bar>(source.Length);
            var summary = new Core.SmaCpuKernel.GridSummary();
            var validator = new CloseReader(cancellation);
            bool needProof = SmaLength > 1 && SmaLength <= source.Length;
            if (AsinValues is null)
            {
                var reader = new OwnedCloseReader();
                summary = Core.SmaCpuKernel.ProcessOwned<Bar, CloseReader, OwnedCloseReader>(
                    source, owned, SmaValues!, SmaLength, ref validator, ref reader);
            }
            else
            {
                source.AsSpan().CopyTo(owned);
                foreach (ref readonly var bar in owned.AsSpan())
                {
                    var close = validator.Read(in bar);
                    if (needProof) summary.Include(close);
                }
            }
            history.TakeOwnedArray(owned);
            if (AsinValues is null && (!needProof || summary.Certifies(SmaLength)))
            {
                UsedSmaFallback = false;
                return;
            }
            ExecutePrepared(owned, summary, cancellation);
        }
        else
        {
            var asin = new AsinKernel(AsinValues!);
            Drain(source, history, ref asin, cancellation);
        }
#endif
    }

#if !NETFRAMEWORK
    private interface IKernel
    {
        void AppendBlock(ReadOnlySpan<Bar> source, Span<Bar> owned, int offset, CancellationToken cancellation);
    }

    // Struct specialization keeps kernel dispatch out of the per-bar loop. Copy,
    // finite validation, arithmetic and final output writes share this traversal.
    private static void Drain<TKernel>(Bar[] source, OwnedBarHistory history,
        ref TKernel kernel, CancellationToken cancellation) where TKernel : struct, IKernel
    {
        if (source.Length >= 16_384)
        {
            cancellation.ThrowIfCancellationRequested();
            var owned = GC.AllocateUninitializedArray<Bar>(source.Length);
            kernel.AppendBlock(source, owned, 0, cancellation);
            history.TakeOwnedArray(owned);
            return;
        }
        for (var offset = 0; offset < source.Length;)
        {
            cancellation.ThrowIfCancellationRequested();
            var count = Math.Min(1024, source.Length - offset);
            var owned = GC.AllocateUninitializedArray<Bar>(count);
            kernel.AppendBlock(source.AsSpan(offset, count), owned, offset, cancellation);
            history.AppendOwnedChunk(owned);
            offset += count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref readonly Bar OwnAndValidate(ReadOnlySpan<Bar> source, Span<Bar> owned,
        int index, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        owned[index] = source[index];
        ref readonly var bar = ref owned[index];
        if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High)
            || !double.IsFinite(bar.Low) || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
            Validation.IndicatorInputDomain.Finite.Validate(in bar);
        return ref bar;
    }

    private readonly struct AsinKernel(double[][] output) : IKernel
    {
        public void AppendBlock(ReadOnlySpan<Bar> source, Span<Bar> owned, int offset, CancellationToken cancellation)
        {
            var values = output[0];
            var present = output[1];
            for (var i = 0; i < source.Length; i++)
            {
                var close = OwnAndValidate(source, owned, i, cancellation).Close;
                var defined = close is >= -1 and <= 1;
                values[offset + i] = defined ? Math.Asin(close) : 0;
                present[offset + i] = defined ? 1 : 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(double close, int index)
        {
            var defined = close is >= -1 and <= 1;
            output[0][index] = defined ? Math.Asin(close) : 0;
            output[1][index] = defined ? 1 : 0;
        }
    }

    private void ExecutePrepared(Bar[] owned, Core.SmaCpuKernel.GridSummary summary,
        CancellationToken cancellation)
    {
        if (SmaLength == 0)
        {
            var asin = new AsinKernel(AsinValues!);
            for (var i = 0; i < owned.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                asin.Append(owned[i].Close, i);
            }
            return;
        }
        bool needProof = SmaLength > 1 && SmaLength <= owned.Length;
        bool certified = !needProof || summary.Certifies(SmaLength);
        if (!certified && summary.CanRefine)
        {
            var certificate = new Core.SmaCpuKernel.Certificate(SmaLength);
            certified = true;
            foreach (ref readonly var bar in owned.AsSpan())
            {
                cancellation.ThrowIfCancellationRequested();
                if (!certificate.Include(bar.Close)) { certified = false; break; }
            }
        }
        UsedSmaFallback = !certified;

        if (AsinValues is not null)
        {
            var consumer = new AsinConsumer(new AsinKernel(AsinValues), 0, _asinOfSma);
            ComputeOwnedMean(owned, ref consumer, cancellation);
        }
        else
        {
            var consumer = new Core.SmaCpuKernel.Identity();
            ComputeOwnedMean(owned, ref consumer, cancellation);
        }
    }

    private readonly struct OwnedCloseReader : Core.SmaCpuKernel.IReader<Bar>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Read(in Bar bar) => bar.Close;
    }

    private void ComputeOwnedMean<TConsumer>(ReadOnlySpan<Bar> bars, ref TConsumer consumer,
        CancellationToken cancellation) where TConsumer : struct, Core.SmaCpuKernel.IConsumer
    {
        var reader = new OwnedCloseReader();
        Span<double> output = SmaValues is null ? Span<double>.Empty : SmaValues;
        if (UsedSmaFallback)
            Core.SmaCpuKernel.ProcessGuarded(bars, output, SmaLength, ref reader, ref consumer, cancellation);
        else
            Core.SmaCpuKernel.ProcessCertified(bars, output, SmaLength, ref reader, ref consumer, cancellation);
    }

    private readonly struct CloseReader(CancellationToken cancellation) : Core.SmaCpuKernel.IReader<Bar>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Read(in Bar bar)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!double.IsFinite(bar.Open) || !double.IsFinite(bar.High)
                || !double.IsFinite(bar.Low) || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                Validation.IndicatorInputDomain.Finite.Validate(in bar);
            return bar.Close;
        }
    }

    private readonly struct AsinConsumer(AsinKernel asin, int offset, bool fromMean) : Core.SmaCpuKernel.IConsumer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Consume(double input, double mean, int index)
        {
            asin.Append(fromMean ? mean : input, offset + index);
            return mean;
        }
    }
#endif
}
