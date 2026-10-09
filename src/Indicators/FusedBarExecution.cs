using System.Runtime.CompilerServices;

namespace OoplesFinance.StockIndicators.Indicators;

// A per-build plan for the two pilot formulas. Only sealed, direct-close nodes
// participate: customer state, domains, projections and graph dependencies keep
// their normal execution and error ordering. No caller buffers are retained.
internal sealed class FusedBarExecution
{
    internal int SmaLength { get; }
    internal double[]? SmaValues { get; }
    internal double[][]? AsinValues { get; }
    internal bool UsedSmaFallback { get; private set; }

    private FusedBarExecution(int count, int smaLength, bool asin)
    {
        SmaLength = smaLength;
        if (smaLength > 0) SmaValues = new double[count];
        if (asin) AsinValues = new[] { new double[count], new double[count] };
    }

    internal static FusedBarExecution? TryCreate(IReadOnlyList<IIndicator> indicators, int count)
    {
#if NETFRAMEWORK
        // The modern certificate is not the Framework SMA arithmetic contract.
        return null;
#else
        var length = 0;
        var asin = false;
        foreach (var indicator in indicators)
        {
            if (indicator is not (Sma or PriceCircularTransform)) return null;
            if (indicator.Source is not null || indicator.Components.Count != 0) return null;
            if (indicator is Sma sma)
            {
                var normalized = Math.Max(1, sma.Length);
                if (length != 0 && length != normalized) return null;
                length = normalized;
            }
            else if (indicator is PriceCircularTransform { Operation: PriceCircularOperation.ArcSine }) asin = true;
            else return null;
        }
        return length != 0 || asin ? new FusedBarExecution(count, length, asin) : null;
#endif
    }

    internal double[][] Values(IIndicator indicator) => indicator is Sma
        ? new[] { SmaValues! } : AsinValues!;

    internal void Execute(Bar[] source, OwnedBarHistory history, CancellationToken cancellation)
    {
#if NETFRAMEWORK
        throw new NotSupportedException("Fused pilot execution requires a modern runtime.");
#else
        if (SmaValues is not null)
        {
            var sma = new SmaKernel(SmaLength, SmaValues);
            if (AsinValues is not null)
            {
                var combined = new CombinedKernel(sma, new AsinKernel(AsinValues));
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
                Core.MovingAverageCore.SimpleMovingAverage(close, SmaValues, SmaLength);
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
        public void Append(in Bar bar, int index)
        {
            var close = bar.Close;
            var defined = close is >= -1 and <= 1;
            output[0][index] = defined ? Math.Asin(close) : 0;
            output[1][index] = defined ? 1 : 0;
        }
    }

    private struct CombinedKernel(SmaKernel sma, AsinKernel asin) : IKernel
    {
        internal SmaKernel Sma = sma;
        public void AppendBlock(ReadOnlySpan<Bar> source, Span<Bar> owned, int offset, CancellationToken cancellation)
        {
            for (var i = 0; i < source.Length; i++)
            {
                ref readonly var bar = ref OwnAndValidate(source, owned, i, cancellation);
                Sma.Append(in bar, offset + i);
                asin.Append(in bar, offset + i);
            }
        }
    }

    private struct SmaKernel : IKernel
    {
        private readonly int _length, _windowBits;
        private readonly double[] _output, _window;
        private int _lowestExponent, _highestExponent, _slot;
        private ulong _significands;
        private double _sum;
        internal bool Certified
        {
            get
            {
                if (_significands == 0) return true;
                if (_lowestExponent == 0) return false;
                // min(exponent) + min(trailing zeros) is a conservative common
                // quantum even when the two minima came from different values.
                var grid = _lowestExponent - 1075 + System.Numerics.BitOperations.TrailingZeroCount(_significands);
                var largest = _highestExponent - 1023;
                return grid >= -512 && largest <= 500 && largest - grid + _windowBits <= 52;
            }
        }

        internal SmaKernel(int length, double[] output)
        {
            _length = length;
            _output = output;
            _window = length > 1 && length <= output.Length ? new double[length] : Array.Empty<double>();
            _windowBits = System.Numerics.BitOperations.Log2((uint)length) + 1;
            _lowestExponent = 2047;
            _highestExponent = 0;
            _significands = 0;
            _slot = 0;
            _sum = 0;
        }

        public void AppendBlock(ReadOnlySpan<Bar> source, Span<Bar> owned, int offset, CancellationToken cancellation)
        {
            var length = _length;
            var output = _output;
            var window = _window;
            var lowest = _lowestExponent;
            var highest = _highestExponent;
            var significands = _significands;
            var slot = _slot;
            var sum = _sum;
            // Indices are bounded by the original array length. The exponent
            // arithmetic is bounded by binary64's 11-bit exponent field.
            unchecked
            {
            for (var i = 0; i < source.Length; i++)
            {
                var value = OwnAndValidate(source, owned, i, cancellation).Close;
                var index = offset + i;
                if (length == 1) { output[index] = value; continue; }
                if (length > output.Length) continue;
                AccumulateCertificate(value, ref lowest, ref highest, ref significands);
                sum += value;
                if (index >= length) sum -= window[slot];
                window[slot] = value;
                if (++slot == length) slot = 0;
                if (index >= length - 1) output[index] = sum / length;
            }
            }
            _lowestExponent = lowest;
            _highestExponent = highest;
            _significands = significands;
            _slot = slot;
            _sum = sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AccumulateCertificate(double value, ref int lowest, ref int highest, ref ulong significands)
        {
            var bits = (ulong)(BitConverter.DoubleToInt64Bits(value) & long.MaxValue);
            if (bits == 0) return;
            var exponent = (int)(bits >> 52);
            lowest = Math.Min(lowest, exponent);
            highest = Math.Max(highest, exponent);
            // Exponent bits above bit 52 do not affect the trailing-zero count.
            significands |= bits | (1UL << 52);
        }

        internal bool CertifyHistory(OwnedBarHistory history)
        {
            var grid = int.MaxValue;
            var largest = int.MinValue;
            for (var chunk = 0; chunk < history.ChunkCount; chunk++)
                foreach (ref readonly var bar in history.Chunk(chunk))
                    if (!Certify(bar.Close, _windowBits, ref grid, ref largest)) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Certify(double value, int windowBits, ref int grid, ref int largest)
        {
            var bits = (ulong)(BitConverter.DoubleToInt64Bits(value) & long.MaxValue);
            if (bits == 0) return true;
            var exponent = (int)(bits >> 52);
            if (exponent is 0 or 2047) return false;
            var significand = (bits & 0xfffffffffffffUL) | (1UL << 52);
            grid = Math.Min(grid, exponent - 1075 + System.Numerics.BitOperations.TrailingZeroCount(significand));
            largest = Math.Max(largest, exponent - 1023);
            // Also bounds the length+1 intermediate before eviction.
            return grid >= -512 && largest <= 500 && largest - grid + windowBits <= 52;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(in Bar bar, int index)
        {
            if (_length == 1) { _output[index] = bar.Close; return; }
            if (_length > _output.Length) return;
            var value = bar.Close;
            AccumulateCertificate(value, ref _lowestExponent, ref _highestExponent, ref _significands);
            _sum += value;
            if (index >= _length) _sum -= _window[_slot];
            _window[_slot] = value;
            if (++_slot == _length) _slot = 0;
            if (index >= _length - 1) _output[index] = _sum / _length;
        }
    }
#endif
}
