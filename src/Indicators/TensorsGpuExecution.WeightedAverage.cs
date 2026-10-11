#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel CompileWeightedAverage()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void weighted_average(__global const double* input, __global const double* unused,
                __global double* output, int count, int period, int normalize_available)
            {
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                if (period == 1) {
                    for (size_t i = start; i < end; i++)
                        output[i] = input[i] == 0.0 ? 0.0 : input[i];
                    return;
                }
                if (normalize_available == 2) {
                    size_t i = start;
                    for (; i < end && i < (size_t)(period - 1); i++) output[i] = 0.0;
                    if (i == end) return;
                    int a = (period + 1) / 2, b = period - a + 1;
                    double mass = (double)a * b;
                    double weighted = 0.0, left = 0.0, right = 0.0;
                    size_t first = i - (size_t)(period - 1);
                    for (int j = 0; j < period; j++) {
                        double value = input[first + (size_t)j];
                        weighted += value * min(j + 1, period - j);
                        if (j < a) left += value;
                        if (j >= period - a) right += value;
                    }
                    for (; i < end; i++) {
                        output[i] = weighted == 0.0 ? 0.0 : weighted / mass;
                        if (i + 1 == end) break;
                        // Two unrounded box sums update the triangular total.
                        // Remove before adding to stay within its certified mass.
                        weighted -= left;
                        right -= input[i - (size_t)(a - 1)];
                        right += input[i + 1];
                        weighted += right;
                        left -= input[i - (size_t)(period - 1)];
                        left += input[i - (size_t)(b - 2)];
                    }
                    return;
                }
                double sum = 0.0, weighted = 0.0;
                double denominator = (double)period * (period + 1) / 2.0;
                size_t first = start >= (size_t)(period - 1) ? start - (size_t)(period - 1) : 0;
                for (size_t j = first; j < start; j++) {
                    sum += input[j];
                    weighted += input[j] * (double)((size_t)period - (start - j));
                }
                for (size_t i = start; i < end; i++) {
                    weighted += input[i] * period;
                    sum += input[i];
                    double mass = denominator;
                    if (normalize_available && i < (size_t)period) {
                        double available = (double)(i + 1);
                        mass = available * (2.0 * period - available + 1.0) / 2.0;
                    }
                    output[i] = weighted == 0.0 ? 0.0 : weighted / mass;
                    // Subtract while sum still contains P terms. This lowers each
                    // coefficient by one, leaving total weight at most T-P.
                    weighted -= sum;
                    if (i >= (size_t)(period - 1)) sum -= input[i - (size_t)(period - 1)];
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "weighted_average"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
