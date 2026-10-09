using System.Numerics;
using System.Runtime.CompilerServices;
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
    // The unpublished output initially owns the validated closes. Delay each guarded
    // result by one window so every rebuild/exact-mean read still sees original input.
    // Memory is O(period), with no mutable source rereads or duplicate full column.
    internal static void ProcessInPlace(double[] values, int length, bool certified,
        CancellationToken cancellation, bool boundedPositive = false)
    {
        cancellation.ThrowIfCancellationRequested();
        if (length == 1) return;
        if (length > values.Length) { Array.Clear(values); return; }
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
                values[expired] = mean;
            }
            values.AsSpan(0, values.Length - warmup).CopyTo(values.AsSpan(warmup));
            values.AsSpan(0, warmup).Clear();
            cancellation.ThrowIfCancellationRequested();
            return;
        }
        var pending = new double[length];
        var reader = new DoubleReader();
        var consumer = new DelayedStore(values, pending);
        if (boundedPositive) ProcessBoundedPositive(values, length, ref consumer, cancellation);
        else ProcessGuarded<double, DoubleReader, DelayedStore>(values, Span<double>.Empty,
            length, ref reader, ref consumer, cancellation);
        consumer.Flush(cancellation);
    }

    internal struct PositiveRangeSummary
    {
        private long _minimum = long.MaxValue, _maximum = long.MinValue;
        public PositiveRangeSummary() { }
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
    private static void ProcessBoundedPositive(double[] values, int length, ref DelayedStore consumer,
        CancellationToken cancellation)
    {
        double sum = 0;
        int untilRebuild = length;
        for (int i = 0; i < values.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            double input = values[i];
            sum += input;
            if (i >= length) sum -= values[i - length];
            consumer.Consume(input, i >= length - 1 ? sum / length : 0, i);
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
    }

    private struct DelayedStore(double[] values, double[] pending) : IConsumer
    {
        private int _slot;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Consume(double input, double mean, int index)
        {
            if (index >= pending.Length) values[index - pending.Length] = pending[_slot];
            pending[_slot] = mean;
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
