#if !NETFRAMEWORK
using System.Numerics;
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedCmf(Bar[] source, double[][] output, MoneyFlowWithDetails indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU CMF input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Four host and eight device columns stay within the shared 56 MiB cap.
            bool retain = source.Length <= MaxRetainedCount * 7 / 12;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length
                    || !_workspace.IsPackedBars || _workspace.PackedColumns != 4))
                { _workspace.Dispose(); _workspace = null; }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true, packedColumns: 4);
            }
            else work = new Workspace(_context, source.Length, packedBars: true, packedColumns: 4);
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
                    packed[i] = latest.High; packed[count + i] = latest.Low;
                    packed[2 * count + i] = latest.Close; packed[3 * count + i] = latest.Volume;
                    ValuesBarExecution.IncludeIntegerGrid(latest.High, ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Low, ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Close, ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Volume, ref volumeGrid, ref maximumVolume);
                }
                if (priceGrid == int.MaxValue) priceGrid = 0;
                if (volumeGrid == int.MaxValue) volumeGrid = 0;
                double priceBound = Math.ScaleB(maximumPrice, -priceGrid);
                double volumeBound = Math.ScaleB(maximumVolume, -volumeGrid);
                if (!(priceBound <= (1L << 40) && volumeBound <= (1L << 40))) RejectCmf();
                int minimumRangeShift = int.MaxValue, maximumRangeShift = 0;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (int slot = 0; slot < 3; slot++)
                        packed[slot * count + i] = Math.ScaleB(packed[slot * count + i], -priceGrid);
                    packed[3 * count + i] = Math.ScaleB(packed[3 * count + i], -volumeGrid);
                    double range = Math.Abs(packed[i] - packed[count + i]);
                    if (range == 0) continue;
                    ulong units = (ulong)range;
                    if (!BitOperations.IsPow2(units)) RejectCmf();
                    int shift = BitOperations.TrailingZeroCount(units);
                    minimumRangeShift = Math.Min(minimumRangeShift, shift);
                    maximumRangeShift = Math.Max(maximumRangeShift, shift);
                }
                if (minimumRangeShift == int.MaxValue) minimumRangeShift = 0;
                int flowGrid = volumeGrid - maximumRangeShift;
                int spread = maximumRangeShift - minimumRangeShift;
                UInt128 productBound = (UInt128)(ulong)priceBound * 4 * (ulong)volumeBound;
                uint terms = indicator.Period <= count ? (uint)indicator.Period : 1;
                // Each published flow is exact on volumeGrid-maxRangeShift.
                // Every P-term signed flow/volume partial sum fits 52 bits;
                // no rounded host output or state series is computed or uploaded.
                if (flowGrid is < -500 or > 500 || spread >= 53
                    || productBound > (((UInt128)1 << 52) >> spread) / terms
                    || (UInt128)(ulong)volumeBound > ((UInt128)1 << 52) / terms) RejectCmf();
                var input = work.PackedInput; var result = work.PackedOutput;
                cancellation.ThrowIfCancellationRequested();
                input.CopyFromHost(packed);
                for (int stage = 0; stage < 2; stage++)
                {
                    int key = 42 + stage;
                    if (!_pointwiseKernels.TryGetValue(key, out var compiled))
                    { compiled = CompileCmf(stage); _pointwiseKernels.Add(key, compiled); }
                    var kernel = compiled.Kernel;
                    kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                    kernel.SetArg(2, count); kernel.SetArg(3, indicator.Period);
                    kernel.SetArg(4, volumeGrid); kernel.SetArg(5, maximumRangeShift);
                    cancellation.ThrowIfCancellationRequested();
                    kernel.Enqueue(new[] { ((ulong)count + 127) / 128 });
                }
                result.CopyToHost(packed);
                for (int slot = 0; slot < 4; slot++)
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

    private static void RejectCmf() =>
        throw new NotSupportedException("CMF inputs do not satisfy the exact GPU flow/window certificate.");

    private CompiledKernel CompileCmf(int stage)
    {
        const string flow = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int volumeGrid, int rangeShift) {
                size_t start = get_global_id(0) * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                for (size_t i = start; i < end; i++) {
                    double high = input[i], low = input[(size_t)count + i];
                    double close = input[2 * (size_t)count + i], volume = input[3 * (size_t)count + i];
                    double range = high - low, balance = (close - low) - (high - close);
                    if (range == 0.0 || balance == 0.0) {
                        result[i] = 0.0; result[(size_t)count + i] = 0.0;
                    }
                    else {
                        int shift = 63 - (int)clz((ulong)fabs(range));
                        double oriented = range < 0.0 ? -balance : balance;
                        result[i] = ldexp(oriented, -shift);
                        result[(size_t)count + i] = volume == 0.0 ? 0.0
                            : ldexp(oriented * volume, volumeGrid - shift);
                    }
                }
            }
            """;
        const string window = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            // Two unsigned limbs, low first. Certified midpoint products fit in 107 bits.
            static inline ulong2 wide_mul(ulong a, ulong b) { return (ulong2)(a * b, mul_hi(a, b)); }
            static inline ulong2 wide_shift(ulong2 a, int shift) {
                if (shift == 0) return a;
                if (shift >= 64) return (ulong2)(0, a.s0 << (shift - 64));
                return (ulong2)(a.s0 << shift, (a.s1 << shift) | (a.s0 >> (64 - shift)));
            }
            static inline int wide_compare(ulong2 a, ulong2 b) {
                if (a.s1 != b.s1) return a.s1 < b.s1 ? -1 : 1;
                return a.s0 == b.s0 ? 0 : a.s0 < b.s0 ? -1 : 1;
            }
            static inline int wide_bits(ulong2 a) { return a.s1 != 0 ? 128 - (int)clz(a.s1) : 64 - (int)clz(a.s0); }
            static inline int midpoint_compare(ulong2 numerator, ulong denominator, ulong midpoint, int power) {
                ulong2 product = wide_mul(denominator, midpoint);
                return power < 0 ? wide_compare(wide_shift(numerator, -power), product)
                    : wide_compare(numerator, wide_shift(product, power));
            }
            // Exact fallback on the binary64 rounding grid. The signed-sum
            // certificate limits both operands to 52 bits, hence numerator shift
            // to at most 105 bits and each doubled remainder to at most 53 bits.
            static inline double integer_divide(ulong2 numerator, ulong denominator) {
                int power = wide_bits(numerator) - (64 - (int)clz(denominator));
                int comparison = power >= 0 ? wide_compare(numerator, wide_shift((ulong2)(denominator, 0), power))
                    : wide_compare(wide_shift(numerator, -power), (ulong2)(denominator, 0));
                if (comparison < 0) power--;
                int shift = 52 - power;
                if (shift >= 0) numerator = wide_shift(numerator, shift);
                else denominator <<= -shift;
                ulong quotient = 0, remainder = 0;
                for (int bit = wide_bits(numerator) - 1; bit >= 0; bit--) {
                    ulong next = bit >= 64 ? (numerator.s1 >> (bit - 64)) & 1UL : (numerator.s0 >> bit) & 1UL;
                    remainder = (remainder << 1) | next;
                    quotient <<= 1;
                    if (remainder >= denominator) { remainder -= denominator; quotient |= 1UL; }
                }
                ulong complement = denominator - remainder;
                if (remainder > complement || (remainder == complement && (quotient & 1UL) != 0)) quotient++;
                return ldexp((double)quotient, power - 52);
            }
            static inline double exact_ratio(long positive, long total) {
                if (positive == 0) return 0.0;
                int negative = (positive < 0) != (total < 0);
                ulong top = positive < 0 ? (ulong)(-positive) : (ulong)positive;
                ulong bottom = total < 0 ? (ulong)(-total) : (ulong)total;
                ulong2 numerator = (ulong2)(top, 0);
                ulong bits = as_ulong((double)top / (double)bottom);
                // Accept a floating estimate only after exact midpoint tests.
                // Every possible financial result here is normal and finite.
                for (int attempt = 0; attempt < 8; attempt++) {
                    int exponent = (int)(bits >> 52) - 1023;
                    ulong significand = (bits & 0xfffffffffffffUL) | (1UL << 52);
                    int boundary = significand == (1UL << 52);
                    ulong lower = boundary ? (significand << 2) - 1 : (significand << 1) - 1;
                    int comparison = midpoint_compare(numerator, bottom, lower, exponent - (boundary ? 54 : 53));
                    if (comparison < 0 || (comparison == 0 && (bits & 1UL) != 0)) { bits--; continue; }
                    comparison = midpoint_compare(numerator, bottom, (significand << 1) + 1, exponent - 53);
                    if (comparison > 0 || (comparison == 0 && (bits & 1UL) != 0)) { bits++; continue; }
                    double result = as_double(bits);
                    return negative ? -result : result;
                }
                double result = integer_divide(numerator, bottom);
                return negative ? -result : result;
            }
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int volumeGrid, int rangeShift) {
                size_t start = get_global_id(0) * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                for (size_t i = start; i < end; i++) {
                    result[2 * (size_t)count + i] = 0.0;
                    result[3 * (size_t)count + i] = 0.0;
                }
                if (period > count || end < (size_t)period) return;
                size_t first = max(start, (size_t)period - 1);
                int flowGrid = volumeGrid - rangeShift;
                long flows = 0, volumes = 0;
                for (size_t j = first - (size_t)period + 1; j <= first; j++) {
                    flows += (long)ldexp(result[(size_t)count + j], -flowGrid);
                    volumes += (long)input[3 * (size_t)count + j];
                }
                for (size_t i = first; i < end; i++) {
                    if (volumes != 0) {
                        // Integer ratios are normal in [2^-52,2^52] in magnitude.
                        // The range shift is at most 41, so final scaling is exact.
                        result[2 * (size_t)count + i] = flows == 0 ? 0.0
                            : ldexp(exact_ratio(flows, volumes), -rangeShift);
                        result[3 * (size_t)count + i] = 1.0;
                    }
                    if (i + 1 < end) {
                        size_t expired = i - (size_t)period + 1;
                        flows -= (long)ldexp(result[(size_t)count + expired], -flowGrid);
                        volumes -= (long)input[3 * (size_t)count + expired];
                        flows += (long)ldexp(result[(size_t)count + i + 1], -flowGrid);
                        volumes += (long)input[3 * (size_t)count + i + 1];
                    }
                }
            }
            """;
        var program = new OpenClProgram(_context, stage == 0 ? flow : window);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "execute"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
