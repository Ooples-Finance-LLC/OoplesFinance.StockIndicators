#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedScaledRange(Bar[] source, double[][] output, ScaledTrueRange indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Required GPU scaled range needs nonempty input.");
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
                // Differences fit 52 bits and are exact after normalization.
                // No range or published ratio is computed by the host.
                if (!(Math.ScaleB(maximum, -grid) <= (1L << 51)))
                    throw new NotSupportedException("Scaled range inputs exceed the exact GPU difference bounds.");
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (int slot = 0; slot < 3; slot++) packed[slot * count + i] = Math.ScaleB(packed[slot * count + i], -grid);
                }
                if (!_pointwiseKernels.TryGetValue(47, out var compiled))
                { compiled = CompileScaledRange(); _pointwiseKernels.Add(47, compiled); }
                var input = work.PackedInput; var result = work.PackedOutput;
                cancellation.ThrowIfCancellationRequested();
                input.CopyFromHost(packed);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                kernel.SetArg(2, count); kernel.SetArg(3, indicator.Divisor); kernel.SetArg(4, grid);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { (ulong)count });
                result.CopyToHost(packed);
                cancellation.ThrowIfCancellationRequested();
                Array.Copy(packed, output[0], count);
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

    private CompiledKernel CompileScaledRange()
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
                // Both operands have at most 53 bits; this scaled numerator fits
                // 105 bits, even at the normal/subnormal rounding boundary.
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
            __kernel void scaled_range(__global const double* input, __global double* result,
                int count, int divisor, int scale) {
                size_t i = get_global_id(0);
                if (i >= (size_t)count) return;
                double high = input[i], low = input[(size_t)count + i];
                double range = high - low;
                if (i > 0) {
                    double previous = input[2 * (size_t)count + i - 1];
                    range = fmax(range, fmax(fabs(high - previous), fabs(low - previous)));
                }
                result[i] = scaled_ratio((long)range, (long)divisor, scale);
                result[(size_t)count + i] = 0.0; result[2 * (size_t)count + i] = 0.0;
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "scaled_range"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
