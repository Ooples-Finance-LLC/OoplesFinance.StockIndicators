using System.Numerics;
using System.Runtime.CompilerServices;
#if !NETFRAMEWORK
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Core;

// CPU fusion boundary: value-type operators specialize the entire hot loop.
// Consumers may use both the input and its mean without materializing an
// intermediate series. This is deliberately internal: speculative execution
// requires a batch owner that can discard/recompute outputs before publication.
internal static class SmaCpuKernel
{
    internal interface IReader<T> { double Read(in T value); }
    internal interface IConsumer { double Consume(double input, double mean, int index); }

    internal readonly struct Identity : IConsumer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Consume(double input, double mean, int index) => mean;
    }

    internal readonly struct DoubleReader : IReader<double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Read(in double value) => value;
    }

    // Same guarded arithmetic for array callers and fused consumers; rejection
    // decisions are made before entering this loop, not by replaying its outputs.
    internal static void ProcessGuarded<T, TReader, TConsumer>(ReadOnlySpan<T> input,
        Span<double> output, int length, ref TReader reader, ref TConsumer consumer,
        CancellationToken cancellation = default)
        where TReader : struct, IReader<T>
        where TConsumer : struct, IConsumer
    {
        double sum = 0;
        var exactRequired = false;
        double roundoff = 0;
        for (var i = 0; i < input.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var value = reader.Read(in input[i]);
            var previousSum = sum;
            sum += value;
            roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
            exactRequired |= ExactMeanAccumulator.SevereCancellation(previousSum, value, sum);
            if (i >= length)
            {
                previousSum = sum;
                var expired = reader.Read(in input[i - length]);
                sum -= expired;
                roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
                exactRequired |= ExactMeanAccumulator.SevereCancellation(previousSum, -expired, sum);
                // If eviction cancels a much larger accumulator, its low-order values were
                // already rounded away. Rebuild before publishing, not on a later periodic bar.
                if (Math.Abs(sum) <= 1e-4 * Math.Max(Math.Abs(expired), Math.Abs(value)))
                {
                    sum = 0;
                    roundoff = 0;
                    for (var j = i - length + 1; j <= i; j++)
                    {
                        if ((j & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                        sum += reader.Read(in input[j]);
                        roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
                    }
                }
            }

            var mean = i >= length - 1 ? sum / length : 0;
            if (i >= length - 1 && (exactRequired || MeanRoundoff.RequiresExact(sum, length, roundoff)))
            {
                var exact = new ExactMeanAccumulator();
                for (var j = i - length + 1; j <= i; j++)
                {
                    if ((j & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                    exact.Add(reader.Read(in input[j]));
                }
                mean = exact.Mean(length);
            }

            var result = consumer.Consume(value, mean, i);
            if (!output.IsEmpty) output[i] = result;

            // Rebuilt from its window every length bars, once the bar's value is taken. A running sum otherwise
            // keeps the rounding error of every value it has ever held: after prices near 100,000 it was still
            // off by 1e-9 at prices near 10, which a deviation from the mean of a tenth turns into 1e-8.
            if (length > 0 && (i + 1) % length == 0)
            {
                sum = 0;
                exactRequired = false;
                roundoff = 0;
                for (var j = i - length + 1; j <= i; j++)
                {
                    if ((j & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                    previousSum = sum;
                    sum += reader.Read(in input[j]);
                    roundoff = MeanRoundoff.AfterAddition(roundoff, sum);
                    exactRequired |= ExactMeanAccumulator.SevereCancellation(previousSum, reader.Read(in input[j]), sum);
                }
            }
        }
    }

#if !NETFRAMEWORK
    // An in-place consumer may observe only the completed mean. Original input
    // slots are reused, and warmup callbacks may run after complete windows.
    internal interface IMeanConsumer { double Consume(double mean, int index); }
    internal readonly struct MeanIdentity : IMeanConsumer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Consume(double mean, int index) => mean;
    }

    // A whole-series grid certificate proves every prefix and window difference
    // is exactly representable. Only then may this SIMD scan replace rolling
    // additions. It is deliberately stricter than the usual period certificate.
    internal static bool TryProcessPrefixInPlace<TConsumer>(double[] values, int length,
        GridSummary summary, ref TConsumer consumer, CancellationToken cancellation)
        where TConsumer : struct, IMeanConsumer
    {
        if (!Vector256.IsHardwareAccelerated || values.Length < 256 || length < 2
            || length > values.Length || !summary.Certifies(values.Length)) return false;
        cancellation.ThrowIfCancellationRequested();
        double carry = 0;
        int i = 0;
        for (; i <= values.Length - 4; i += 4)
        {
            cancellation.ThrowIfCancellationRequested();
            var v = Vector256.LoadUnsafe(ref values[0], (nuint)i);
            if (Avx2.IsSupported)
            {
                v += Avx.Blend(Avx2.Permute4x64(v, 0x90), Vector256<double>.Zero, 1);
                v += Avx.Blend(Avx2.Permute4x64(v, 0x40), Vector256<double>.Zero, 3);
            }
            else
            {
                v += Vector256.Create(0d, v.GetElement(0), v.GetElement(1), v.GetElement(2));
                v += Vector256.Create(0d, 0d, v.GetElement(0), v.GetElement(1));
            }
            v += Vector256.Create(carry);
            v.StoreUnsafe(ref values[0], (nuint)i);
            carry = v.GetElement(3);
        }
        for (; i < values.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            values[i] = carry += values[i];
        }

        // Traverse backwards: every needed earlier prefix remains intact until
        // its final read. No duplicate prefix array or period-sized ring is needed.
        int end = values.Length;
        var divisor = Vector256.Create((double)length);
        for (; end - length >= 4; end -= 4)
        {
            cancellation.ThrowIfCancellationRequested();
            int first = end - 4;
            var means = (Vector256.LoadUnsafe(ref values[0], (nuint)first)
                - Vector256.LoadUnsafe(ref values[0], (nuint)(first - length))) / divisor;
            if (typeof(TConsumer) == typeof(MeanIdentity))
                means.StoreUnsafe(ref values[0], (nuint)first);
            else for (int lane = 0; lane < 4; lane++)
                values[first + lane] = consumer.Consume(means.GetElement(lane), first + lane);
        }
        for (i = end - 1; i >= length; i--)
        {
            cancellation.ThrowIfCancellationRequested();
            values[i] = consumer.Consume((values[i] - values[i - length]) / length, i);
        }
        values[length - 1] = consumer.Consume(values[length - 1] / length, length - 1);
        for (i = 0; i < length - 1; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            values[i] = consumer.Consume(0, i);
        }
        return true;
    }

    // Certify the already-owned close column in SIMD batches. Floating-point
    // arithmetic is unchanged: these reductions operate only on IEEE encodings.
    internal static void Summarize(ReadOnlySpan<double> values, out GridSummary grid,
        out PositiveRangeSummary positive, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        grid = new GridSummary();
        positive = new PositiveRangeSummary();
        int i = 0;
        if (Vector.IsHardwareAccelerated && values.Length >= Vector<long>.Count)
        {
            var mask = new Vector<long>(long.MaxValue);
            var minimum = mask;
            var nonzeroMinimum = mask;
            var maximum = Vector<long>.Zero;
            var combined = Vector<long>.Zero;
            int width = Vector<long>.Count;
            for (; i <= values.Length - width; i += width)
            {
                cancellation.ThrowIfCancellationRequested();
                var bits = Vector.AsVectorInt64(new Vector<double>(values.Slice(i, width)));
                var magnitude = bits & mask;
                minimum = Vector.Min(minimum, bits);
                maximum = Vector.Max(maximum, magnitude);
                nonzeroMinimum = Vector.Min(nonzeroMinimum,
                    Vector.ConditionalSelect(Vector.Equals(magnitude, Vector<long>.Zero), mask, magnitude));
                combined |= magnitude;
            }
            long min = long.MaxValue, nonzeroMin = long.MaxValue, max = 0, all = 0;
            for (int lane = 0; lane < width; lane++)
            {
                min = Math.Min(min, minimum[lane]);
                nonzeroMin = Math.Min(nonzeroMin, nonzeroMinimum[lane]);
                max = Math.Max(max, maximum[lane]);
                all |= combined[lane];
            }
            grid = GridSummary.FromMagnitudes(nonzeroMin, max, all);
            // A negative signed minimum (including -0) rejects the positive
            // proof. Otherwise magnitude extrema are exactly the signed extrema.
            positive.Include(BitConverter.Int64BitsToDouble(min));
            positive.Include(BitConverter.Int64BitsToDouble(max));
        }
        for (; i < values.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            grid.Include(values[i]);
            positive.Include(values[i]);
        }
    }

    // The unpublished output initially owns the validated closes. Proven inputs
    // compact into expired slots; unproven inputs delay results in an O(period)
    // ring so rebuild/exact-mean reads still see original input. Neither path
    // rereads mutable source bars or allocates a duplicate full column.
    internal static void ProcessInPlace(double[] values, int length, bool certified,
        CancellationToken cancellation, bool boundedPositive = false)
    {
        var consumer = new MeanIdentity();
        ProcessInPlace(values, length, certified, ref consumer, cancellation, boundedPositive);
    }

    internal static void ProcessInPlace<TConsumer>(double[] values, int length, bool certified,
        ref TConsumer consumer, CancellationToken cancellation, bool boundedPositive = false)
        where TConsumer : struct, IMeanConsumer
    {
        cancellation.ThrowIfCancellationRequested();
        if (length == 1 || length > values.Length)
        {
            for (int i = 0; i < values.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                values[i] = consumer.Consume(length == 1 ? values[i] : 0, i);
            }
            return;
        }
        if (certified)
        {
            // Compact results overwrite only closes already evicted from the sum.
            // This needs no ring; restore public warmup alignment after all reads.
            int warmup = length - 1;
            double sum = 0;
            for (int i = 0; i < warmup; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                sum += values[i];
            }
            for (int i = warmup; i < values.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                sum += values[i];
                double mean = sum / length;
                int expired = i - warmup;
                sum -= values[expired];
                values[expired] = consumer.Consume(mean, i);
            }
            values.AsSpan(0, values.Length - warmup).CopyTo(values.AsSpan(warmup));
            for (int i = 0; i < warmup; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                values[i] = consumer.Consume(0, i);
            }
            cancellation.ThrowIfCancellationRequested();
            return;
        }
        if (boundedPositive)
        {
            ProcessBoundedPositive(values, length, ref consumer, cancellation);
            return;
        }
        var pending = new double[length];
        var reader = new DoubleReader();
        var delayed = new DelayedStore<TConsumer>(values, pending, consumer);
        ProcessGuarded<double, DoubleReader, DelayedStore<TConsumer>>(values, Span<double>.Empty,
            length, ref reader, ref delayed, cancellation);
        delayed.Flush(cancellation);
        consumer = delayed.Consumer;
    }

    internal struct PositiveRangeSummary
    {
        private long _minimum = long.MaxValue, _maximum = long.MinValue;
        public PositiveRangeSummary() { }
        internal void Merge(PositiveRangeSummary other)
        {
            _minimum = Math.Min(_minimum, other._minimum);
            _maximum = Math.Max(_maximum, other._maximum);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Include(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            _minimum = Math.Min(_minimum, bits);
            _maximum = Math.Max(_maximum, bits);
        }
        // 2^-256 <= min <= max <= 2^256, max/min <= 2, and 2 <= period <= 4096.
        // Positive IEEE encodings separated by one exponent step differ by 2^52.
        internal readonly bool Certifies(int period) => period is >= 2 and <= 4096
            && _minimum >= 0x2ff0000000000000L && _maximum <= 0x4ff0000000000000L
            && _maximum >= _minimum && _maximum - _minimum <= (1L << 52);
    }

    // Requires the bounded-positive or period grid proof. Split at rebuilds,
    // preserving the scalar guarded loop's exact sum, eviction and rebuild order.
    // Capture preceding windows before any writes. Worker-local pooled scratch
    // retains original closes while results go directly to final positions.
    // Only scratch is pooled: published values always retain owned storage.
    internal static void ProcessRebasedParallel(double[] values, int length,
        int participants, CancellationToken cancellation)
    {
        if (length < 2 || length > 4096) throw new ArgumentOutOfRangeException(nameof(length));
        if (participants < 1 || (values.Length - length) / length < participants)
            throw new ArgumentOutOfRangeException(nameof(participants));
        cancellation.ThrowIfCancellationRequested();
        int intervals = (values.Length - length) / length;
        var edges = new double[participants * length];
        int Begin(int chunk) => length + (int)((long)intervals * chunk / participants) * length;
        double firstSum = 0;
        for (int i = 0; i < length; i++) firstSum += values[i];
        double firstMean = firstSum / length;
        int scratchLength = 0;
        for (int chunk = 0; chunk < participants; chunk++)
        {
            values.AsSpan(Begin(chunk) - length, length).CopyTo(edges.AsSpan(chunk * length, length));
            int end = chunk == participants - 1 ? values.Length : Begin(chunk + 1);
            scratchLength = Math.Max(scratchLength, end - Begin(chunk) + length);
        }
        AiDotNet.Tensors.Helpers.CpuParallelSettings.LightweightParallel(participants, participants,
            () => System.Buffers.ArrayPool<double>.Shared.Rent(scratchLength), (chunk, scratch) =>
        {
            int start = Begin(chunk);
            int end = chunk == participants - 1 ? values.Length : Begin(chunk + 1);
            var input = scratch.AsSpan(0, end - start + length);
            edges.AsSpan(chunk * length, length).CopyTo(input);
            values.AsSpan(start, end - start).CopyTo(input.Slice(length));
            var output = values.AsSpan(start, end - start);
            double sum = 0;
            for (int j = 0; j < length; j++) sum += input[j];
            int untilRebuild = length;
            for (int i = length; i < input.Length; i++)
            {
                if (cancellation.IsCancellationRequested) return;
                sum += input[i];
                sum -= input[i - length];
                output[i - length] = sum / length;
                if (--untilRebuild == 0)
                {
                    if (i + 1 < input.Length)
                    {
                        sum = 0;
                        for (int j = i - length + 1; j <= i; j++) sum += input[j];
                    }
                    untilRebuild = length;
                }
            }
        }, scratch => System.Buffers.ArrayPool<double>.Shared.Return(scratch));
        cancellation.ThrowIfCancellationRequested();
        values.AsSpan(0, length - 1).Clear();
        values[length - 1] = firstMean;
        cancellation.ThrowIfCancellationRequested();
    }

    // Preserve the guarded loop's floating-point operations and rebuild cadence.
    // This proof ONLY removes guard bookkeeping; it is not the grid certificate.
    // Between rebuilds at most 3L additions/subtractions affect error. With M<=2m,
    // L<=4096 and u=2^-53, accumulated error is < Lm/2, so each full sum is >=Lm/2
    // and <=2(L+1)M. Outward bound increments are conservatively <=(L+1)M*2^-50;
    // hence bound/abs(sum) <=12(L+1)*2^-50 < 4.37e-11, below RequiresExact's 1e-10.
    // Positive additions cannot cancel, and eviction retains >1/6 of its largest
    // operand: neither 1e-4 cancellation/rebuild trigger can fire. Exponent bounds
    // exclude overflow, subnormal means and outward-bound underflow. Unqualified
    // input always uses the original guarded path.
    private static void ProcessBoundedPositive<TConsumer>(double[] values, int length,
        ref TConsumer consumer, CancellationToken cancellation) where TConsumer : struct, IMeanConsumer
    {
        double sum = 0;
        for (int i = 0; i < length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            sum += values[i];
        }
        double firstMean = consumer.Consume(sum / length, length - 1);
        // The first scheduled rebuild repeats precisely the additions above.
        // Retain that sum; all later rebuilds keep the original cadence/order.
        int untilRebuild = length;
        for (int i = length; i < values.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            sum += values[i];
            int expired = i - length;
            sum -= values[expired];
            // Only the expired close is overwritten. Rebuilds start one slot
            // later, so they continue to read original input without a ring.
            values[expired] = consumer.Consume(sum / length, i);
            if (--untilRebuild == 0)
            {
                sum = 0;
                for (int j = i - length + 1; j <= i; j++)
                {
                    if ((j & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                    sum += values[j];
                }
                untilRebuild = length;
            }
        }
        values.AsSpan(0, values.Length - length).CopyTo(values.AsSpan(length));
        for (int i = 0; i < length - 1; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            values[i] = consumer.Consume(0, i);
        }
        values[length - 1] = firstMean;
        cancellation.ThrowIfCancellationRequested();
    }

    private struct DelayedStore<TConsumer>(double[] values, double[] pending, TConsumer consumer) : IConsumer
        where TConsumer : struct, IMeanConsumer
    {
        internal TConsumer Consumer = consumer;
        private int _slot;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Consume(double input, double mean, int index)
        {
            if (index >= pending.Length) values[index - pending.Length] = pending[_slot];
            pending[_slot] = Consumer.Consume(mean, index);
            if (++_slot == pending.Length) _slot = 0;
            return mean;
        }
        internal void Flush(CancellationToken cancellation)
        {
            for (int i = values.Length - pending.Length; i < values.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                values[i] = pending[_slot];
                if (++_slot == pending.Length) _slot = 0;
            }
        }
    }

    internal struct GridSummary
    {
        private int _lowest = 2047, _highest;
        private ulong _significands;
        public GridSummary() { }
        internal void Merge(GridSummary other)
        {
            _lowest = Math.Min(_lowest, other._lowest);
            _highest = Math.Max(_highest, other._highest);
            _significands |= other._significands;
        }
        internal static GridSummary FromMagnitudes(long nonzeroMinimum, long maximum, long combined) => new()
        {
            _lowest = nonzeroMinimum == long.MaxValue ? 2047 : (int)(nonzeroMinimum >> 52),
            _highest = (int)(maximum >> 52),
            _significands = (ulong)combined | (maximum == 0 ? 0 : 1UL << 52)
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Include(double value)
        {
            var bits = (ulong)(BitConverter.DoubleToInt64Bits(value) & long.MaxValue);
            if (bits == 0) return;
            var exponent = (int)(bits >> 52);
            _lowest = Math.Min(_lowest, exponent);
            _highest = Math.Max(_highest, exponent);
            _significands |= bits | (1UL << 52);
        }
        internal readonly bool CanRefine => _lowest > 0 && _highest < 2047
            && _lowest - 1023 >= -512 && _highest - 1023 <= 500;
        internal readonly bool Certifies(int length) => _significands == 0 || (CanRefine
            && _lowest - 1075 + BitOperations.TrailingZeroCount(_significands) >= -512
            && _highest - _lowest + 52 - BitOperations.TrailingZeroCount(_significands)
                + BitOperations.Log2((uint)length) + 1 <= 52);
    }

    // Single-output finite SMA can speculate cheap arithmetic while owning its
    // input, then replace rejected results with the guarded loop. Eviction reads
    // owned history; no separate ring buffer or mutable caller reread is needed.
    internal static GridSummary ProcessOwned<T, TValidator, TReader>(ReadOnlySpan<T> source,
        Span<T> owned, Span<double> output, int length, ref TValidator validator, ref TReader reader)
        where TValidator : struct, IReader<T> where TReader : struct, IReader<T>
    {
        var summary = new GridSummary();
        if (length == 1 || length > source.Length)
        {
            for (var i = 0; i < source.Length; i++)
            {
                owned[i] = source[i];
                var value = validator.Read(in owned[i]);
                output[i] = length == 1 ? value : 0;
            }
            return summary;
        }
        double sum = 0;
        var warmup = length - 1;
        for (var i = 0; i < warmup; i++)
        {
            owned[i] = source[i];
            var value = validator.Read(in owned[i]);
            summary.Include(value);
            sum += value;
            output[i] = 0;
        }
        for (var i = warmup; i < source.Length; i++)
        {
            owned[i] = source[i];
            var value = validator.Read(in owned[i]);
            summary.Include(value);
            sum += value;
            output[i] = sum / length;
            sum -= reader.Read(in owned[i - warmup]);
        }
        return summary;
    }

    internal struct GridFacts
    {
        private int _grid = int.MaxValue, _largest = int.MinValue;
        private bool _rejected;
        public GridFacts() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Include(double value)
        {
            if (_rejected) return;
            var bits = (ulong)(BitConverter.DoubleToInt64Bits(value) & long.MaxValue);
            if (bits == 0) return;
            var exponent = (int)(bits >> 52);
            if (exponent is 0 or 2047) { _rejected = true; return; }
            var significand = (bits & 0xfffffffffffffUL) | (1UL << 52);
            _grid = Math.Min(_grid, exponent - 1075 + BitOperations.TrailingZeroCount(significand));
            _largest = Math.Max(_largest, exponent - 1023);
            if (_grid < -512 || _largest > 500) _rejected = true;
        }

        internal readonly bool Certifies(int length) => !_rejected && (_grid == int.MaxValue
            || _largest - _grid + BitOperations.Log2((uint)length) + 1 <= 52);
    }

    internal struct Certificate(int length)
    {
        private GridFacts _facts = new();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Include(double value)
        {
            _facts.Include(value);
            return _facts.Certifies(length);
        }
    }

    // Requires an owned input whose grid was certified for this period (unless
    // period 1 or all-warmup). No ring, recertification or intermediate output.
    internal static void ProcessCertified<T, TReader, TConsumer>(ReadOnlySpan<T> input,
        Span<double> output, int length, ref TReader reader, ref TConsumer consumer,
        CancellationToken cancellation)
        where TReader : struct, IReader<T>
        where TConsumer : struct, IConsumer
    {
        var warmup = length > input.Length ? input.Length : length - 1;
        double sum = 0;
        for (var i = 0; i < warmup; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var value = reader.Read(in input[i]);
            if (length <= input.Length) sum += value;
            var result = consumer.Consume(value, 0, i);
            if (!output.IsEmpty) output[i] = result;
        }
        if (length == 1)
        {
            for (var i = 0; i < input.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var value = reader.Read(in input[i]);
                var result = consumer.Consume(value, value, i);
                if (!output.IsEmpty) output[i] = result;
            }
            return;
        }
        for (var i = warmup; i < input.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var value = reader.Read(in input[i]);
            sum += value;
            var result = consumer.Consume(value, sum / length, i);
            if (!output.IsEmpty) output[i] = result;
            sum -= reader.Read(in input[i - warmup]);
        }
    }

    // State has a bounded lifetime; copies must not run as independent executions.
    // The ring is owned by this execution, never by the caller's source buffer.
    internal struct State
    {
        internal readonly int Length;
        private readonly double[] _window;
        private readonly int _windowBits;
        private int _slot, _count, _lowest, _highest;
        private ulong _significands;
        private double _sum;

        internal State(int length, int totalCount)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            if (totalCount < 0) throw new ArgumentOutOfRangeException(nameof(totalCount));
            this = default;
            Length = length;
            _window = length > 1 && length <= totalCount ? new double[length] : [];
            _windowBits = BitOperations.Log2((uint)length) + 1;
            _lowest = 2047;
        }

        internal readonly bool Certified
        {
            get
            {
                if (_significands == 0) return true;
                if (_lowest is 0 || _highest == 2047) return false;
                var grid = _lowest - 1075 + BitOperations.TrailingZeroCount(_significands);
                var largest = _highest - 1023;
                return grid >= -512 && largest <= 500 && largest - grid + _windowBits <= 52;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Next(double value)
        {
            if (Length == 1) return value;
            if (_window.Length == 0) return 0;
            var bits = (ulong)(BitConverter.DoubleToInt64Bits(value) & long.MaxValue);
            if (bits != 0)
            {
                var exponent = (int)(bits >> 52);
                _lowest = Math.Min(_lowest, exponent);
                _highest = Math.Max(_highest, exponent);
                _significands |= bits | (1UL << 52);
            }
            var ready = _count == Length;
            var mean = Step(value, ready ? _window[_slot] : 0, ref _sum, Length);
            _window[_slot] = value;
            if (++_slot == Length) _slot = 0;
            if (!ready) ++_count;
            return _count == Length ? mean : 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Step(double value, double expired, ref double sum, int length)
    {
        sum += value;
        sum -= expired;
        return sum / length;
    }

    // Source ownership, input projection/validation, rolling SMA and consumption
    // all execute in this single loop, including across caller-selected blocks.
    // An empty output elides the SMA series when only downstream outputs are requested.
    // Results are provisional until State.Certified (or a refined proof) succeeds.
    internal static void Process<T, TReader, TConsumer>(ReadOnlySpan<T> source, Span<T> owned,
        Span<double> output, ref State state, ref TReader reader, ref TConsumer consumer)
        where TReader : struct, IReader<T>
        where TConsumer : struct, IConsumer
    {
        if (owned.Length < source.Length || (!output.IsEmpty && output.Length < source.Length))
            throw new ArgumentException("Kernel buffers must cover the source block.");
        // Keep recurrence fields local so the JIT can promote them to registers.
        // On an operator exception the batch owner must abandon this execution.
        var local = state;
        for (var i = 0; i < source.Length; i++)
        {
            owned[i] = source[i];
            var value = reader.Read(in owned[i]);
            var mean = local.Next(value);
            var result = consumer.Consume(value, mean, i);
            if (!output.IsEmpty) output[i] = result;
        }
        state = local;
    }

    // Contiguous-input adapter uses the input itself as the eviction window:
    // no ring allocation. Rejection leaves output AND consumer state untouched.
    internal static bool TryProcess<TConsumer>(ReadOnlySpan<double> input, Span<double> output,
        int length, ref TConsumer consumer) where TConsumer : struct, IConsumer
    {
        if (length < 2 || output.Length < input.Length || input.Overlaps(output)) return false;
        var certificate = new Certificate(length);
        foreach (var value in input)
            if (!certificate.Include(value)) return false;
        double sum = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var mean = Step(input[i], i >= length ? input[i - length] : 0, ref sum, length);
            output[i] = consumer.Consume(input[i], i >= length - 1 ? mean : 0, i);
        }
        return true;
    }
#endif
}
