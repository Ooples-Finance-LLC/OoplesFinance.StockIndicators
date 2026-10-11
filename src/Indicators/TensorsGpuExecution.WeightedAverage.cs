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
                __global double* output, int count, int period)
            {
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                if (period == 1) {
                    for (size_t i = start; i < end; i++)
                        output[i] = input[i] == 0.0 ? 0.0 : input[i];
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
                    output[i] = weighted == 0.0 ? 0.0 : weighted / denominator;
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
