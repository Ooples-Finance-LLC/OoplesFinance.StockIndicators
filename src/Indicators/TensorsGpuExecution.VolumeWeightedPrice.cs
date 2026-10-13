#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedVolumePrice(Bar[] source, double[][] output, VolumeWeightedPrice indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU weighted-price input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Five host and ten device columns share the existing 56 MiB cap.
            bool retain = source.Length <= MaxRetainedCount * 7 / 15;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length
                    || !_workspace.IsPackedBars || _workspace.PackedColumns != 5))
                { _workspace.Dispose(); _workspace = null; }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true, packedColumns: 5);
            }
            else work = new Workspace(_context, source.Length, packedBars: true, packedColumns: 5);
            try
            {
                int count = source.Length, priceGrid = int.MaxValue, volumeGrid = int.MaxValue;
                double maximumPrice = 0, maximumVolume = 0;
                var packed = work.PackedHost;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    packed[i] = indicator.TypicalPrice ? latest.High : 0;
                    packed[count + i] = indicator.TypicalPrice ? latest.Low : 0;
                    packed[2 * count + i] = latest.Close; packed[3 * count + i] = latest.Volume;
                    packed[4 * count + i] = indicator.StartDate is DateTime anchor && latest.Time < anchor ? 0 : 1;
                    ValuesBarExecution.IncludeIntegerGrid(packed[i], ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(packed[count + i], ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Close, ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Volume, ref volumeGrid, ref maximumVolume);
                }
                if (priceGrid == int.MaxValue) priceGrid = 0;
                if (volumeGrid == int.MaxValue) volumeGrid = 0;
                double priceBound = Math.ScaleB(maximumPrice, -priceGrid);
                double volumeBound = Math.ScaleB(maximumVolume, -volumeGrid);
                if (!(priceBound <= (1L << 40) && volumeBound <= (1L << 40))) RejectVolumePrice();
                uint factor = indicator.TypicalPrice ? 3u : 1u;
                uint terms = indicator.Period is int period ? period <= count ? (uint)period : 1u : (uint)count;
                // Every signed product/window sum and scaled denominator fits
                // 52 bits. The host computes bounds, never products or outputs.
                if ((UInt128)(ulong)priceBound * factor * (ulong)volumeBound > ((UInt128)1 << 52) / terms
                    || (UInt128)(ulong)volumeBound * factor > ((UInt128)1 << 52) / terms) RejectVolumePrice();
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (int slot = 0; slot < 3; slot++)
                        packed[slot * count + i] = Math.ScaleB(packed[slot * count + i], -priceGrid);
                    packed[3 * count + i] = Math.ScaleB(packed[3 * count + i], -volumeGrid);
                }
                var input = work.PackedInput; var result = work.PackedOutput;
                cancellation.ThrowIfCancellationRequested();
                input.CopyFromHost(packed);
                for (int stage = 0; stage < 2; stage++)
                {
                    int key = 44 + stage;
                    if (!_pointwiseKernels.TryGetValue(key, out var compiled))
                    { compiled = CompileVolumePrice(stage); _pointwiseKernels.Add(key, compiled); }
                    var kernel = compiled.Kernel;
                    kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                    kernel.SetArg(2, count); kernel.SetArg(3, indicator.Period ?? 0);
                    kernel.SetArg(4, (int)factor); kernel.SetArg(5, priceGrid);
                    cancellation.ThrowIfCancellationRequested();
                    kernel.Enqueue(new[] { ((ulong)count + 127) / 128 });
                }
                result.CopyToHost(packed);
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

    private static void RejectVolumePrice() =>
        throw new NotSupportedException("Weighted-price inputs do not satisfy the exact GPU product/window certificate.");

    private CompiledKernel CompileVolumePrice(int stage)
    {
        const string products = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int factor, int priceGrid) {
                size_t block = get_global_id(0), start = block * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                long weighted = 0, mass = 0;
                for (size_t i = start; i < end; i++) {
                    long product = 0;
                    if (input[4 * (size_t)count + i] != 0.0) {
                        long price = (long)input[2 * (size_t)count + i];
                        if (factor == 3) price += (long)input[i] + (long)input[(size_t)count + i];
                        long volume = (long)input[3 * (size_t)count + i];
                        product = price * volume;
                        if (period == 0) { weighted += product; mass += volume; }
                    }
                    result[2 * (size_t)count + i] = (double)product;
                }
                result[3 * (size_t)count + block] = (double)weighted;
                result[4 * (size_t)count + block] = (double)mass;
            }
            """;
        const string windows = """
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
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int factor, int priceGrid) {
                size_t block = get_global_id(0), start = block * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                for (size_t i = start; i < end; i++) { result[i] = 0.0; result[(size_t)count + i] = 0.0; }
                if (period > count) return;
                long weighted = 0, mass = 0;
                size_t origin = start;
                int available = 0;
                if (period == 0) {
                    for (size_t b = 0; b < block; b++) {
                        weighted += (long)result[3 * (size_t)count + b];
                        mass += (long)result[4 * (size_t)count + b];
                    }
                }
                else {
                    while (origin > 0 && available < period - 1) {
                        origin--;
                        if (input[4 * (size_t)count + origin] != 0.0) available++;
                    }
                    for (size_t i = origin; i < start; i++) if (input[4 * (size_t)count + i] != 0.0) {
                        weighted += (long)result[2 * (size_t)count + i];
                        mass += (long)input[3 * (size_t)count + i];
                    }
                }
                for (size_t i = start; i < end; i++) {
                    if (input[4 * (size_t)count + i] == 0.0) continue;
                    weighted += (long)result[2 * (size_t)count + i];
                    mass += (long)input[3 * (size_t)count + i];
                    if (period != 0) available++;
                    if ((period == 0 || available == period) && mass != 0) {
                        result[i] = scaled_ratio(weighted, mass * factor, priceGrid);
                        result[(size_t)count + i] = 1.0;
                    }
                    if (period != 0 && available == period) {
                        while (input[4 * (size_t)count + origin] == 0.0) origin++;
                        weighted -= (long)result[2 * (size_t)count + origin];
                        mass -= (long)input[3 * (size_t)count + origin];
                        origin++; available--;
                    }
                }
            }
            """;
        var program = new OpenClProgram(_context, stage == 0 ? products : windows);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "execute"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
