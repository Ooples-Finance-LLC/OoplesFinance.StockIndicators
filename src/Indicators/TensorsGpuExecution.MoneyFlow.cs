#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedMoneyFlow(Bar[] source, double[][] output, WindowMoneyFlowIndex indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU MoneyFlow input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Same three host and six device columns as packed Vortex.
            bool retain = source.Length <= MaxRetainedCount * 7 / 9;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length || !_workspace.IsPackedBars))
                {
                    _workspace.Dispose(); _workspace = null;
                }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true);
            }
            else work = new Workspace(_context, source.Length, packedBars: true);
            try
            {
                int count = source.Length, priceGrid = int.MaxValue, volumeGrid = int.MaxValue;
                double maximumPrice = 0, maximumVolume = 0;
                bool hasOutput = (long)indicator.Period + indicator.SuppressedBars < count;
                var packed = work.PackedHost;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    double price = hasOutput ? Helpers.ExactTypicalPrice.Calculate(latest.High, latest.Low, latest.Close) : latest.Close;
                    packed[i] = price; packed[count + i] = latest.Volume; packed[2 * count + i] = 0;
                    if (hasOutput)
                    {
                        ValuesBarExecution.IncludeIntegerGrid(price, ref priceGrid, ref maximumPrice);
                        ValuesBarExecution.IncludeIntegerGrid(latest.Volume, ref volumeGrid, ref maximumVolume);
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if (priceGrid == int.MaxValue) priceGrid = 0;
                if (volumeGrid == int.MaxValue) volumeGrid = 0;
                if (hasOutput)
                {
                    double priceBound = Math.ScaleB(maximumPrice, -priceGrid);
                    double volumeBound = Math.ScaleB(maximumVolume, -volumeGrid);
                    if (!Core.MoneyFlowIntegerCertificate.TryGetProduct(priceBound, volumeBound, out var product)
                        || !Core.MoneyFlowIntegerCertificate.FitsInt64(product, indicator.Period))
                        throw new NotSupportedException("Money flow inputs do not satisfy the exact GPU signed-integer certificate.");
                    for (int i = 0; i < count; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        packed[i] = Math.ScaleB(packed[i], -priceGrid);
                        packed[count + i] = Math.ScaleB(packed[count + i], -volumeGrid);
                    }
                }
                if (!_pointwiseKernels.TryGetValue(34, out var compiled))
                {
                    compiled = CompileMoneyFlow(); _pointwiseKernels.Add(34, compiled);
                }
                var input = work.PackedInput; var result = work.PackedOutput;
                input.CopyFromHost(packed);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                kernel.SetArg(2, count); kernel.SetArg(3, indicator.Period);
                kernel.SetArg(4, indicator.MinimumTotalOne ? 1 : 0); kernel.SetArg(5, indicator.SuppressedBars);
                kernel.SetArg(6, priceGrid + volumeGrid);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { ((ulong)count + 63) / 64 });
                // Upload is blocking; reuse its host storage for packed readback.
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

    private CompiledKernel CompileMoneyFlow()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            // Two unsigned limbs, low first. Products are at most 118 bits.
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
            // certificate limits the divisor to 63 bits, hence numerator shift
            // to at most 116 bits and each doubled remainder to at most 64 bits.
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
                ulong2 numerator = wide_mul(top, 100UL);
                ulong bits = as_ulong((100.0 * (double)top) / (double)bottom);
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
            static inline void accumulate(__global const double* prices, __global const double* volumes,
                size_t i, int sign, __private long* positive, __private long* negative) {
                long price = (long)prices[i], previous = (long)prices[i-1];
                long flow = price * (long)volumes[i];
                if (sign < 0) flow = -flow;
                if (price > previous) *positive += flow;
                else if (price < previous) *negative += flow;
            }
            __kernel void money_flow(__global const double* input, __global double* result,
                int count, int period, int minimum, int suppressed, int scale)
            {
                __global const double* prices = input;
                __global const double* volumes = input + (size_t)count;
                __global double* output = result;
                __global double* presence = result + (size_t)count;
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                for (size_t i = start; i < end; i++) result[2 * (size_t)count + i] = 0.0;
                if ((ulong)period + (ulong)suppressed >= (ulong)count) {
                    for (size_t i = start; i < end; i++) { output[i] = 0.0; presence[i] = 0.0; }
                    return;
                }
                long positive = 0, negative = 0;
                size_t first = start >= (size_t)period ? start - (size_t)period + 1 : 1;
                for (size_t j = first; j < start; j++) accumulate(prices, volumes, j, 1, &positive, &negative);
                for (size_t i = start; i < end; i++) {
                    if (i > 0) accumulate(prices, volumes, i, 1, &positive, &negative);
                    int defined = (ulong)i >= (ulong)period + (ulong)suppressed;
                    double value = 0.0;
                    if (defined) {
                        long total = positive + negative;
                        int belowOne = total <= 0 || (scale < 0 && (-scale >= 63 || total < (1L << -scale)));
                        if (minimum && belowOne) value = 0.0;
                        else if (!minimum && negative == 0) value = 100.0;
                        else if (total == 0) value = -INFINITY;
                        else value = exact_ratio(positive, total);
                    }
                    output[i] = value; presence[i] = defined ? 1.0 : 0.0;
                    if (i >= (size_t)period) accumulate(prices, volumes, i - (size_t)period + 1, -1, &positive, &negative);
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "money_flow"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
