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
                __global double* output, int count, int period)
            {
                size_t i = get_global_id(0);
                if (i >= (size_t)count) return;
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
                if ((length & 1) != 0) { output[i] = sorted[length / 2]; return; }
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
