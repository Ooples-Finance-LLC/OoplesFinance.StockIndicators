#if !NETFRAMEWORK
using System.Numerics;
using System.Runtime.CompilerServices;

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

    internal struct Certificate(int length)
    {
        private readonly int _windowBits = BitOperations.Log2((uint)length) + 1;
        private int _grid = int.MaxValue, _largest = int.MinValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Include(double value)
        {
            var bits = (ulong)(BitConverter.DoubleToInt64Bits(value) & long.MaxValue);
            if (bits == 0) return true;
            var exponent = (int)(bits >> 52);
            if (exponent is 0 or 2047) return false;
            var significand = (bits & 0xfffffffffffffUL) | (1UL << 52);
            _grid = Math.Min(_grid, exponent - 1075 + BitOperations.TrailingZeroCount(significand));
            _largest = Math.Max(_largest, exponent - 1023);
            return _grid >= -512 && _largest <= 500 && _largest - _grid + _windowBits <= 52;
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
}
#endif
