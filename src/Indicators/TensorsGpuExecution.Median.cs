#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel CompileMedian()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void median_window(__global const double* input, __global const double* unused,
                __global double* output, int count, int period, int average_warmup, int quarters)
            {
                size_t i = get_global_id(0);
                if (i >= (size_t)count) return;
                if (average_warmup && i + 1 < (size_t)period)
                {
                    // The host certifies every startup partial sum as exact.
                    double total = 0.0;
                    for (size_t j = 0; j <= i; j++) total += input[j];
                    output[i] = total == 0.0 ? 0.0 : total / (double)(i + 1);
                    return;
                }
                size_t start = i + 1 > (size_t)period ? i + 1 - (size_t)period : 0;
                double sorted[64];
                int length = 0;
                for (size_t j = start; j <= i; j++)
                {
                    double value = input[j];
                    // Match List<double>.BinarySearch insertion during prefix warmup.
                    // In particular, equal signed zeros insert at the found midpoint.
                    int low = 0, high = length - 1;
                    while (low <= high)
                    {
                        int middle = low + ((high - low) >> 1);
                        if (sorted[middle] < value) low = middle + 1;
                        else if (sorted[middle] > value) high = middle - 1;
                        else { low = middle; break; }
                    }
                    for (int at = length; at > low; at--) sorted[at] = sorted[at - 1];
                    sorted[low] = value;
                    length++;
                }
                int rank = (length - 1) * quarters, index = rank / 4, remainder = rank % 4;
                if (remainder == 0) { output[i] = sorted[index]; return; }
                if (quarters != 2)
                {
                    // A host certificate makes each weighted term and sum exact.
                    double total = (double)(4 - remainder) * sorted[index] + (double)remainder * sorted[index + 1];
                    output[i] = total == 0.0 ? 0.0 : total * 0.25;
                    return;
                }
                double a = sorted[length / 2 - 1], b = sorted[length / 2];
                double total = a + b;
                output[i] = total == 0.0 ? 0.0 : isinf(total) ? a * 0.5 + b * 0.5 : total * 0.5;
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "median_window"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
