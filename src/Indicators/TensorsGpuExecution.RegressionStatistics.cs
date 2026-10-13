#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedRegressionStatistics(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int period = indicator is WindowRegressionStatistics statistics ? statistics.Period
            : ((RegressionWithFinalLine)indicator).Period;
        bool historical = indicator is RegressionWithFinalLine;
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Ten host columns and twenty device columns share the 56 MiB cap.
            bool retain = source.Length <= MaxRetainedCount * 7 / 30;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length
                    || !_workspace.IsPackedBars || _workspace.PackedColumns != 10))
                { _workspace.Dispose(); _workspace = null; }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true, packedColumns: 10);
            }
            else work = new Workspace(_context, source.Length, packedBars: true, packedColumns: 10);
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
                    packed[i] = latest.Close;
                    ValuesBarExecution.IncludeIntegerGrid(latest.Close, ref grid, ref maximum);
                }
                if (grid == int.MaxValue) grid = 0;
                // P*B<=2^20 gives variance<=2^40 and |covariance|<=2^27
                // for P<=256. R-squared products fit 56 bits; global fitted
                // numerators fit signed Int64 even at int-indexed source limits.
                if (count == 0 || period > 256 || !(Math.ScaleB(maximum, -grid) <= (1L << 20) / period))
                    throw new NotSupportedException("Regression inputs exceed the exact GPU period or integer-moment bounds.");
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    packed[i] = Math.ScaleB(packed[i], -grid);
                }
                if (!_pointwiseKernels.TryGetValue(48, out var compiled))
                { compiled = CompileRegressionStatistics(); _pointwiseKernels.Add(48, compiled); }
                var input = work.PackedInput; var result = work.PackedOutput;
                cancellation.ThrowIfCancellationRequested();
                input.CopyFromHost(packed);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                kernel.SetArg(2, count); kernel.SetArg(3, period); kernel.SetArg(4, grid);
                kernel.SetArg(5, historical ? 1 : 0);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { (ulong)count });
                result.CopyToHost(packed);
                cancellation.ThrowIfCancellationRequested();
                for (int slot = 0; slot < 10; slot++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Array.Copy(packed, slot * count, output[slot], 0, count);
                }
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

    private CompiledKernel CompileRegressionStatistics()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            static inline ulong2 wide_mul(ulong a, ulong b) { return (ulong2)(a * b, mul_hi(a, b)); }
            static inline int wide_compare(ulong2 a, ulong2 b) {
                if (a.s1 != b.s1) return a.s1 < b.s1 ? -1 : 1;
                return a.s0 == b.s0 ? 0 : a.s0 < b.s0 ? -1 : 1;
            }
            static inline ulong2 wide_subtract(ulong2 a, ulong2 b) {
                return (ulong2)(a.s0 - b.s0, a.s1 - b.s1 - (a.s0 < b.s0));
            }
            static inline ulong2 shifted(ulong a, int shift) {
                if (shift == 0) return (ulong2)(a, 0);
                if (shift >= 64) return (ulong2)(0, a << (shift - 64));
                return (ulong2)(a << shift, a >> (64 - shift));
            }
            static inline double scaled_ratio(long weighted, long mass, int scale) {
                if (weighted == 0) return 0.0;
                int negative = (weighted < 0) != (mass < 0);
                ulong top = weighted < 0 ? (ulong)(-weighted) : (ulong)weighted;
                ulong bottom = mass < 0 ? (ulong)(-mass) : (ulong)mass;
                int power = (64 - (int)clz(top)) - (64 - (int)clz(bottom));
                if (power >= 0 ? top < (bottom << power) : (top << -power) < bottom) power--;
                int exponent = power + scale;
                if (exponent > 1023) return negative ? -INFINITY : INFINITY;
                if (exponent < -1075) return negative ? -0.0 : 0.0;
                int grid = max(exponent - 52, -1074), shift = scale - grid;
                // Regression bounds keep the normalized ratio below 2^52 and
                // its denominator below 2^56, so this shift fits 109 bits.
                ulong2 numerator = shifted(top, shift);
                ulong quotient = 0, remainder = 0;
                int certified = 0;
                double estimate = ldexp((double)top / (double)bottom, shift);
                if (estimate >= 0.0 && estimate < 0x1p54) {
                    quotient = (ulong)estimate;
                    for (int attempt = 0; attempt < 8; attempt++) {
                        ulong2 product = wide_mul(quotient, bottom);
                        if (wide_compare(product, numerator) > 0) { quotient--; continue; }
                        ulong2 residual = wide_subtract(numerator, product);
                        if (residual.s1 != 0 || residual.s0 >= bottom) { quotient++; continue; }
                        remainder = residual.s0; certified = 1; break;
                    }
                }
                if (!certified) {
                    quotient = remainder = 0;
                    int bits = numerator.s1 != 0 ? 128 - (int)clz(numerator.s1) : 64 - (int)clz(numerator.s0);
                    for (int bit = bits - 1; bit >= 0; bit--) {
                        ulong next = bit >= 64 ? (numerator.s1 >> (bit - 64)) & 1UL : (numerator.s0 >> bit) & 1UL;
                        remainder = (remainder << 1) | next;
                        quotient <<= 1;
                        if (remainder >= bottom) { remainder -= bottom; quotient |= 1UL; }
                    }
                }
                ulong complement = bottom - remainder;
                if (remainder > complement || (remainder == complement && (quotient & 1UL) != 0)) quotient++;
                double value = ldexp((double)quotient, grid);
                return negative ? -value : value;
            }
            static inline ulong2 multiply_small(ulong2 a, ulong b) {
                ulong2 low = wide_mul(a.s0, b);
                return (ulong2)(low.s0, low.s1 + a.s1 * b);
            }
            static inline ulong2 root_product(ulong root, ulong denominator) {
                return multiply_small(wide_mul(root, root), denominator);
            }
            static inline double scaled_deviation(ulong variance, ulong n, int scale) {
                if (variance == 0) return 0.0;
                ulong denominator = n * n;
                int power = (64 - (int)clz(variance)) - (64 - (int)clz(denominator));
                if (power >= 0 ? variance < (denominator << power) : (variance << -power) < denominator) power--;
                int exponent = power + 2 * scale;
                int root_exponent = exponent >= 0 ? exponent / 2 : (exponent - 1) / 2;
                if (root_exponent > 1023) return INFINITY;
                if (root_exponent < -1075) return 0.0;
                int grid = max(root_exponent - 52, -1074), shift = 2 * (scale - grid);
                // variance<=2^40 and n^2<=2^16 keep this numerator below
                // 2^123. Squared midpoint products and 4*numerator fit 128 bits.
                ulong2 numerator = shifted(variance, shift);
                double estimate = ldexp(sqrt((double)variance) / (double)n, scale - grid);
                ulong root = 0;
                int certified = 0;
                if (estimate >= 0.0 && estimate < 0x1p54) {
                    root = (ulong)estimate;
                    for (int attempt = 0; attempt < 8; attempt++) {
                        if (wide_compare(root_product(root, denominator), numerator) > 0) { root--; continue; }
                        if (wide_compare(root_product(root + 1, denominator), numerator) <= 0) { root++; continue; }
                        certified = 1; break;
                    }
                }
                if (!certified) {
                    ulong low = 0, high = 1UL << 53;
                    while (low < high) {
                        ulong middle = low + (high - low + 1) / 2;
                        if (wide_compare(root_product(middle, denominator), numerator) <= 0) low = middle;
                        else high = middle - 1;
                    }
                    root = low;
                }
                ulong midpoint = 2 * root + 1;
                ulong2 four = (ulong2)(numerator.s0 << 2, (numerator.s1 << 2) | (numerator.s0 >> 62));
                int comparison = wide_compare(four, root_product(midpoint, denominator));
                if (comparison > 0 || (comparison == 0 && (root & 1UL) != 0)) root++;
                return ldexp((double)root, grid);
            }
            static inline long4 moments(__global const double* input, int first, int end) {
                long sum = 0, squares = 0, weighted = 0;
                for (int j = first; j <= end; j++) {
                    long value = (long)input[j];
                    sum += value; squares += value * value; weighted += (j - first) * value;
                }
                long n = end - first + 1;
                return (long4)(sum, n * squares - sum * sum, 2 * weighted - (n - 1) * sum, n);
            }
            __kernel void regression_statistics(__global const double* input, __global double* result,
                int count, int period, int scale, int historical) {
                int i = (int)get_global_id(0);
                if (i >= count) return;
                for (int slot = 0; slot < 10; slot++) result[(size_t)slot * count + i] = 0.0;
                long4 fit = moments(input, max(0, i - period + 1), i);
                long n = fit.s3;
                if (!historical) result[5 * (size_t)count + i] = 1.0;
                if (n >= 2 && (!historical || n == period)) {
                    long spread = n * n - 1, denominator = n * spread;
                    long basis = fit.s0 * spread, covariance3 = 3 * fit.s2;
                    result[i] = scaled_ratio(2 * covariance3, denominator, scale);
                    long position = historical ? n - 2L * i - 3 : -n - 1;
                    result[(size_t)count + i] = scaled_ratio(basis + covariance3 * position, denominator, scale);
                    result[2 * (size_t)count + i] = scaled_deviation((ulong)fit.s1, (ulong)n, scale);
                    if (fit.s1 != 0) {
                        result[3 * (size_t)count + i] = scaled_ratio(covariance3 * fit.s2, fit.s1 * spread, 0);
                        result[8 * (size_t)count + i] = 1.0;
                    }
                    result[5 * (size_t)count + i] = result[6 * (size_t)count + i] = result[7 * (size_t)count + i] = 1.0;
                    if (!historical) {
                        result[4 * (size_t)count + i] = scaled_ratio(basis + covariance3 * (n - 1), denominator, scale);
                        result[9 * (size_t)count + i] = 1.0;
                    }
                }
                // Each final-window work item owns its line cell. It computes
                // the final fit on-device, avoiding cross-work-item write races.
                if (historical && count >= period && i >= count - period) {
                    fit = moments(input, count - period, count - 1);
                    long spread = fit.s3 * fit.s3 - 1;
                    long position = 2L * (i - (count - period)) - (fit.s3 - 1);
                    result[4 * (size_t)count + i] = scaled_ratio(fit.s0 * spread + 3 * fit.s2 * position,
                        fit.s3 * spread, scale);
                    result[9 * (size_t)count + i] = 1.0;
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "regression_statistics"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
