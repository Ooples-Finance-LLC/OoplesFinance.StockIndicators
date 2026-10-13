#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedForceIndex(Bar[] source, double[][] output, SeededForceIndex indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU Force Index input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            bool retain = source.Length <= MaxRetainedCount * 7 / 9;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length
                    || !_workspace.IsPackedBars || _workspace.PackedColumns != 3))
                { _workspace.Dispose(); _workspace = null; }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true);
            }
            else work = new Workspace(_context, source.Length, packedBars: true);
            try
            {
                int count = source.Length, priceGrid = int.MaxValue, volumeGrid = int.MaxValue;
                double maximumPrice = 0, maximumVolume = 0;
                bool active = indicator.Period < count;
                var packed = work.PackedHost;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    packed[i] = latest.Close; packed[count + i] = latest.Volume; packed[2 * count + i] = 0;
                    if (active)
                    {
                        ValuesBarExecution.IncludeIntegerGrid(latest.Close, ref priceGrid, ref maximumPrice);
                        ValuesBarExecution.IncludeIntegerGrid(latest.Volume, ref volumeGrid, ref maximumVolume);
                    }
                }
                if (priceGrid == int.MaxValue) priceGrid = 0;
                if (volumeGrid == int.MaxValue) volumeGrid = 0;
                int scale = priceGrid + volumeGrid;
                if (active)
                {
                    double prices = Math.ScaleB(maximumPrice, -priceGrid), volumes = Math.ScaleB(maximumVolume, -volumeGrid);
                    if (!(prices <= (1L << 40) && volumes <= (1L << 40)) || scale is < -500 or > 500)
                        RejectForceIndex();
                    UInt128 product = (UInt128)(ulong)prices * 2 * (ulong)volumes;
                    // Raw products and every signed seed partial sum are exact
                    // integers. Recurrence rounding is performed on the device.
                    if (product > ((UInt128)1 << 40) || product > ((UInt128)1 << 52) / (uint)indicator.Period)
                        RejectForceIndex();
                    for (int i = 0; i < count; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        packed[i] = Math.ScaleB(packed[i], -priceGrid);
                        packed[count + i] = Math.ScaleB(packed[count + i], -volumeGrid);
                    }
                }
                if (!_pointwiseKernels.TryGetValue(41, out var compiled))
                { compiled = CompileForceIndex(); _pointwiseKernels.Add(41, compiled); }
                var input = work.PackedInput; var result = work.PackedOutput;
                cancellation.ThrowIfCancellationRequested();
                input.CopyFromHost(packed);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                kernel.SetArg(2, count); kernel.SetArg(3, indicator.Period); kernel.SetArg(4, scale);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { ((ulong)count + 127) / 128 });
                result.CopyToHost(packed);
                // Each work item owns its failure flag. No partially supported
                // trajectory is published, and no host output fallback is used.
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (packed[2 * count + i] != 0) RejectForceIndex();
                }
                for (int slot = 0; slot < 2; slot++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Array.Copy(packed, slot * count, output[slot], 0, count);
                }
                cancellation.ThrowIfCancellationRequested();
                return latest;
            }
            catch
            {
                if (retain) { _workspace = null; work.Dispose(); }
                throw;
            }
            finally { if (!retain) work.Dispose(); }
        }
    }

    private static void RejectForceIndex() =>
        throw new NotSupportedException("Force Index inputs exceed the exact GPU product, seed or recurrence bounds.");

    private CompiledKernel CompileForceIndex()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            static inline int bits(ulong2 a) { return a.s1 ? 128 - (int)clz(a.s1) : 64 - (int)clz(a.s0); }
            static inline int compare(ulong2 a, ulong2 b) {
                if (a.s1 != b.s1) return a.s1 < b.s1 ? -1 : 1;
                return a.s0 == b.s0 ? 0 : a.s0 < b.s0 ? -1 : 1;
            }
            static inline ulong2 subtract(ulong2 a, ulong2 b) {
                return (ulong2)(a.s0 - b.s0, a.s1 - b.s1 - (a.s0 < b.s0));
            }
            static inline ulong2 shift(ulong2 a, int n, __private int* ok) {
                if (bits(a) == 0 || n == 0) return a;
                if (n < 0 || n >= 128 || bits(a) + n > 128) { *ok = 0; return (ulong2)(0, 0); }
                if (n >= 64) return (ulong2)(0, a.s0 << (n - 64));
                return (ulong2)(a.s0 << n, (a.s1 << n) | (a.s0 >> (64 - n)));
            }
            static inline ulong2 add(ulong2 a, ulong2 b, __private int* ok) {
                ulong low = a.s0 + b.s0, high = a.s1 + b.s1;
                if (high < a.s1) *ok = 0;
                ulong carried = high + (low < a.s0);
                if (carried < high) *ok = 0;
                return (ulong2)(low, carried);
            }
            static inline ulong2 term(double value, ulong weight, __private int* exponent, __private int* negative) {
                ulong raw = as_ulong(value), field = (raw >> 52) & 2047UL;
                ulong mantissa = (raw & 0xfffffffffffffUL) | (field ? (1UL << 52) : 0UL);
                *exponent = field ? (int)field - 1075 : -1074;
                *negative = (int)(raw >> 63);
                return (ulong2)(mantissa * weight, mul_hi(mantissa, weight));
            }
            // Round an exact signed dyadic / positive integer once, including
            // halfway ties, subnormal results and negative underflow zero.
            static inline double ratio(ulong2 numerator, ulong denominator, int scale, int negative, __private int* ok) {
                if (bits(numerator) == 0) return 0.0;
                ulong2 bottom = (ulong2)(denominator, 0);
                int power = bits(numerator) - bits(bottom);
                int order = power >= 0 ? compare(numerator, shift(bottom, power, ok))
                    : compare(shift(numerator, -power, ok), bottom);
                if (order < 0) power--;
                int exponent = power + scale;
                if (exponent > 1023) { *ok = 0; return 0.0; }
                if (exponent < -1075) return negative ? -0.0 : 0.0;
                int grid = max(exponent - 52, -1074), amount = scale - grid;
                if (amount >= 0) numerator = shift(numerator, amount, ok);
                else bottom = shift(bottom, -amount, ok);
                if (!*ok) return 0.0;
                ulong quotient = 0;
                ulong2 remainder = (ulong2)(0, 0);
                for (int bit = bits(numerator) - 1; bit >= 0; bit--) {
                    ulong next = bit >= 64 ? (numerator.s1 >> (bit - 64)) & 1UL : (numerator.s0 >> bit) & 1UL;
                    int carry = (int)(remainder.s1 >> 63);
                    remainder = (ulong2)((remainder.s0 << 1) | next, (remainder.s1 << 1) | (remainder.s0 >> 63));
                    quotient <<= 1;
                    if (carry || compare(remainder, bottom) >= 0) {
                        remainder = subtract(remainder, bottom); quotient |= 1UL;
                    }
                }
                int rounding = compare(remainder, subtract(bottom, remainder));
                if (rounding > 0 || (rounding == 0 && (quotient & 1UL))) quotient++;
                double value = ldexp((double)quotient, grid);
                return negative ? -value : value;
            }
            static inline double mean(double previous, double raw, int period, __private int* ok) {
                int ae, be, an, bn;
                ulong2 a = term(previous, (ulong)period - 1UL, &ae, &an);
                ulong2 b = term(raw, 2UL, &be, &bn);
                if (bits(a) == 0) return ratio(b, (ulong)period + 1UL, be, bn, ok);
                if (bits(b) == 0) return ratio(a, (ulong)period + 1UL, ae, an, ok);
                int exponent = min(ae, be);
                a = shift(a, ae - exponent, ok); b = shift(b, be - exponent, ok);
                if (!*ok) return 0.0;
                ulong2 numerator;
                int negative;
                if (an == bn) { numerator = add(a, b, ok); negative = an; }
                else if (compare(a, b) >= 0) { numerator = subtract(a, b); negative = an; }
                else { numerator = subtract(b, a); negative = bn; }
                return ratio(numerator, (ulong)period + 1UL, exponent, negative, ok);
            }
            __kernel void force_index(__global const double* input, __global double* result,
                int count, int period, int scale) {
                size_t start = get_global_id(0) * 128, end = min(start + (size_t)128, (size_t)count);
                if (start >= (size_t)count) return;
                for (size_t i = start; i < end; i++) {
                    result[i] = 0.0; result[(size_t)count + i] = 0.0; result[2 * (size_t)count + i] = 0.0;
                }
                if (period >= count) return;
                // Each worker replays its prefix so every EMA stage has the
                // same rounding history. No host-computed states are uploaded.
                double seed = 0.0, value = 0.0;
                int ok = 1;
                for (size_t i = 1; i < end; i++) {
                    double raw = (input[i] - input[i-1]) * input[(size_t)count + i];
                    if (i <= (size_t)period) seed += raw;
                    if (i < (size_t)period) continue;
                    if (i == (size_t)period) {
                        long integer = (long)seed;
                        ulong magnitude = integer < 0 ? (ulong)(-integer) : (ulong)integer;
                        value = ratio((ulong2)(magnitude, 0), (ulong)period, scale, integer < 0, &ok);
                    }
                    else value = mean(value, ldexp(raw, scale), period, &ok);
                    if (!ok) { result[2 * (size_t)count + start] = 1.0; return; }
                    if (i >= start) { result[i] = value; result[(size_t)count + i] = 1.0; }
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "force_index"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
