using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Indicators;

// A per-build plan for the two pilot formulas, including Asin(SMA(close)).
// Only sealed nodes participate: customer state, domains, projections and other dependencies keep
// their normal execution and error ordering. No caller buffers are retained.
internal sealed class FusedBarExecution
{
    private readonly bool _asinOfSma;
    internal int SmaLength { get; }
    internal double[]? SmaValues { get; }
    internal double[][]? AsinValues { get; }
    internal bool UsedSmaFallback { get; private set; }

    private FusedBarExecution(int count, int smaLength, bool asin, bool asinOfSma, bool publishSma)
    {
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

    internal double[][] Values(IIndicator indicator) => indicator is Sma
        ? new[] { SmaValues! } : AsinValues!;

    internal Builder.IndicatorExecutionInfo Execute(Bar[] source, OwnedBarHistory history,
        CancellationToken cancellation, Builder.IndicatorExecutionBackend backend)
    {
        cancellation.ThrowIfCancellationRequested();
#if !NETFRAMEWORK
        // Automatic GPU selection stays disabled until end-to-end evidence supports it.
        if (backend == Builder.IndicatorExecutionBackend.Gpu)
        {
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
        if (SmaLength > 0)
        {
            var sma = new SmaKernel(SmaLength, source.Length, SmaValues);
            if (AsinValues is not null)
            {
                var combined = new CombinedKernel(sma, new AsinKernel(AsinValues), _asinOfSma);
                Drain(source, history, ref combined, cancellation);
                sma = combined.Sma;
            }
            else Drain(source, history, ref sma, cancellation);
            if (!sma.Certified && !sma.CertifyHistory(history))
            {
                UsedSmaFallback = true;
                var close = new double[source.Length];
                var offset = 0;
                for (var chunk = 0; chunk < history.ChunkCount; chunk++)
                {
                    var bars = history.Chunk(chunk);
                    for (var i = 0; i < bars.Length; i++) close[offset + i] = bars[i].Close;
                    offset += bars.Length;
                }
                var means = SmaValues ?? new double[source.Length];
                Core.MovingAverageCore.SimpleMovingAverage(close, means, SmaLength);
                if (_asinOfSma)
                {
                    var consumer = new AsinKernel(AsinValues!);
                    for (var i = 0; i < means.Length; i++) consumer.Append(means[i], i);
                }
            }
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

    private struct CombinedKernel(SmaKernel sma, AsinKernel asin, bool fromMean) : IKernel
    {
        internal SmaKernel Sma = sma;
        public void AppendBlock(ReadOnlySpan<Bar> source, Span<Bar> owned, int offset, CancellationToken cancellation)
        {
            Sma.AppendCombined(source, owned, offset, cancellation, asin, fromMean);
        }
    }

    private struct SmaKernel : IKernel
    {
        private Core.SmaCpuKernel.State _state;
        private readonly double[]? _output;
        internal bool Certified => _state.Certified;

        internal SmaKernel(int length, int count, double[]? output)
        {
            _state = new Core.SmaCpuKernel.State(length, count);
            _output = output;
        }

        private readonly Span<double> OutputBlock(int offset, int count) =>
            _output is null ? Span<double>.Empty : _output.AsSpan(offset, count);

        public void AppendBlock(ReadOnlySpan<Bar> source, Span<Bar> owned, int offset, CancellationToken cancellation)
        {
            // The shared CPU loop owns the traversal, including the input and
            // output operators. No intermediate close or average series.
            var reader = new CloseReader(cancellation);
            var consumer = new Core.SmaCpuKernel.Identity();
            Core.SmaCpuKernel.Process(source, owned, OutputBlock(offset, source.Length),
                ref _state, ref reader, ref consumer);
        }

        internal void AppendCombined(ReadOnlySpan<Bar> source, Span<Bar> owned, int offset,
            CancellationToken cancellation, AsinKernel asin, bool fromMean)
        {
            var reader = new CloseReader(cancellation);
            var consumer = new AsinConsumer(asin, offset, fromMean);
            Core.SmaCpuKernel.Process(source, owned, OutputBlock(offset, source.Length),
                ref _state, ref reader, ref consumer);
        }

        internal bool CertifyHistory(OwnedBarHistory history)
        {
            var certificate = new Core.SmaCpuKernel.Certificate(_state.Length);
            for (var chunk = 0; chunk < history.ChunkCount; chunk++)
                foreach (ref readonly var bar in history.Chunk(chunk))
                    if (!certificate.Include(bar.Close)) return false;
            return true;
        }
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
