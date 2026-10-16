#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedSeededAtr(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU seeded ATR input.");
        int period = indicator switch
        {
            SeededAverageTrueRange atr => atr.Period,
            NormalizedSeededAverageTrueRange atr => atr.Period,
            AverageTrueRangeWithDetails atr => atr.Period,
            _ => throw new InvalidOperationException("Unqualified GPU seeded ATR plan.")
        };
        int mode = indicator is AverageTrueRangeWithDetails ? 2 : indicator is NormalizedSeededAverageTrueRange ? 1 : 0;
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Seven host and fourteen device columns share the 56 MiB cap.
            bool retain = source.Length <= MaxRetainedCount * 7 / 21;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length
                    || !_workspace.IsPackedBars || _workspace.PackedColumns != 7))
                { _workspace.Dispose(); _workspace = null; }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true, packedColumns: 7);
            }
            else work = new Workspace(_context, source.Length, packedBars: true, packedColumns: 7);
            try
            {
                int count = source.Length, grid = int.MaxValue;
                double maximum = 0;
                var packed = work.PackedHost;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    packed[i] = latest.High; packed[count + i] = latest.Low; packed[2 * count + i] = latest.Close;
                    ValuesBarExecution.IncludeIntegerGrid(latest.High, ref grid, ref maximum);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Low, ref grid, ref maximum);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Close, ref grid, ref maximum);
                }
                if (grid == int.MaxValue) grid = 0;
                double bound = Math.ScaleB(maximum, -grid);
                if (!(bound <= (1L << 40)) || grid is < -500 or > 500) RejectSeededAtr();
                // Normalized ranges and the seed sum are exact integers. The
                // host only certifies bounds; the device computes every range.
                if (period < count && (UInt128)(ulong)bound * 2 * (uint)period > ((UInt128)1 << 52)) RejectSeededAtr();
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (int slot = 0; slot < 3; slot++) packed[slot * count + i] = Math.ScaleB(packed[slot * count + i], -grid);
                    for (int slot = 3; slot < 7; slot++) packed[slot * count + i] = 0;
                }
                if (!_pointwiseKernels.TryGetValue(46, out var compiled))
                { compiled = CompileSeededAtr(); _pointwiseKernels.Add(46, compiled); }
                var input = work.PackedInput; var result = work.PackedOutput;
                cancellation.ThrowIfCancellationRequested();
                input.CopyFromHost(packed);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                kernel.SetArg(2, count); kernel.SetArg(3, period); kernel.SetArg(4, grid); kernel.SetArg(5, mode);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { ((ulong)count + 127) / 128 });
                result.CopyToHost(packed);
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (packed[6 * count + i] != 0) RejectSeededAtr();
                }
                for (int slot = 0; slot < output.Length; slot++)
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

    private static void RejectSeededAtr() =>
        throw new NotSupportedException("Seeded ATR inputs exceed the exact GPU range, seed or recurrence bounds.");

    private CompiledKernel CompileSeededAtr()
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
            static inline int certified_divide(ulong2 numerator, ulong2 bottom,
                __private ulong* quotient, __private ulong2* remainder) {
                double estimate = (ldexp((double)numerator.s1, 64) + (double)numerator.s0)
                    / (ldexp((double)bottom.s1, 64) + (double)bottom.s0);
                if (!(estimate >= 0.0 && estimate < 0x1p54)) return 0;
                ulong q = (ulong)estimate;
                // Leave room for ordinary conversion/division error. This is
                // only a guess: the checks below do not rely on its accuracy.
                q = q > 2UL ? q - 2UL : 0UL;
                ulong carry = mul_hi(bottom.s0, q), high = bottom.s1 * q;
                if (mul_hi(bottom.s1, q) != 0 || high + carry < high) return 0;
                ulong2 product = (ulong2)(bottom.s0 * q, high + carry);
                if (compare(product, numerator) > 0) return 0;
                ulong2 rest = subtract(numerator, product);
                for (int i = 0; i < 4 && compare(rest, bottom) >= 0; i++) {
                    rest = subtract(rest, bottom); q++;
                }
                // An exact, nonoverflowing product and a remainder in [0,d)
                // certify floor(n/d), even on a device with a poor estimate.
                if (compare(rest, bottom) >= 0) return 0;
                *quotient = q; *remainder = rest;
                return 1;
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
                if (!certified_divide(numerator, bottom, &quotient, &remainder)) {
                    for (int bit = bits(numerator) - 1; bit >= 0; bit--) {
                        ulong next = bit >= 64 ? (numerator.s1 >> (bit - 64)) & 1UL : (numerator.s0 >> bit) & 1UL;
                        int carry = (int)(remainder.s1 >> 63);
                        remainder = (ulong2)((remainder.s0 << 1) | next, (remainder.s1 << 1) | (remainder.s0 >> 63));
                        quotient <<= 1;
                        if (carry || compare(remainder, bottom) >= 0) {
                            remainder = subtract(remainder, bottom); quotient |= 1UL;
                        }
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
                ulong2 b = term(raw, 1UL, &be, &bn);
                if (bits(a) == 0) return ratio(b, (ulong)period, be, 0, ok);
                if (bits(b) == 0) return ratio(a, (ulong)period, ae, 0, ok);
                int exponent = min(ae, be);
                a = shift(a, ae - exponent, ok); b = shift(b, be - exponent, ok);
                if (!*ok) return 0.0;
                return ratio(add(a, b, ok), (ulong)period, exponent, 0, ok);
            }
            __kernel void seeded_atr(__global const double* input, __global double* result,
                int count, int period, int scale, int mode) {
                size_t start = get_global_id(0) * 128, end = min(start + (size_t)128, (size_t)count);
                if (start >= (size_t)count) return;
                for (size_t i = start; i < end; i++)
                    for (int slot = 0; slot < 7; slot++) result[(size_t)slot * count + i] = 0.0;
                // Each worker replays the original seed and rounded recurrence.
                // The host supplies only normalized HLC, never state or outputs.
                double seed = 0.0, value = 0.0;
                int ok = 1;
                for (size_t i = 1; i < end; i++) {
                    double high = input[i], low = input[(size_t)count + i];
                    double previous = input[2 * (size_t)count + i - 1];
                    double raw = fmax(high - low, fmax(fabs(high - previous), fabs(low - previous)));
                    double range = ldexp(raw, scale);
                    int present = i >= (size_t)period;
                    if (period < count) {
                        if (i <= (size_t)period) seed += raw;
                        if (i == (size_t)period) value = ratio((ulong2)((ulong)seed, 0), (ulong)period, scale, 0, &ok);
                        else if (present) value = mean(value, range, period, &ok);
                    }
                    if (!ok) { result[6 * (size_t)count + start] = 1.0; return; }
                    if (i < start) continue;
                    double close = input[2 * (size_t)count + i], percent = 0.0;
                    int defined = present && close != 0.0;
                    if (mode != 0 && defined && value != 0.0) {
                        int exponent, negative;
                        ulong2 top = term(value, 100UL, &exponent, &negative);
                        percent = ratio(top, (ulong)fabs(close), exponent - scale, close < 0.0, &ok);
                        if (!ok) { result[6 * (size_t)count + start] = 1.0; return; }
                    }
                    if (mode == 2) {
                        result[i] = range; result[(size_t)count + i] = value;
                        result[2 * (size_t)count + i] = percent; result[3 * (size_t)count + i] = 1.0;
                        result[4 * (size_t)count + i] = present ? 1.0 : 0.0;
                        result[5 * (size_t)count + i] = defined ? 1.0 : 0.0;
                    }
                    else result[i] = mode == 1 ? percent : value;
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "seeded_atr"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
