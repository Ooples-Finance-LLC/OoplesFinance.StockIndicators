#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel CompilePathRatio()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void path_ratio(__global const double* input, __global const double* unused,
                __global double* output, int count, int period, __global double* presence, int signed_percent)
            {
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                if (period >= count) {
                    for (size_t i = start; i < end; i++) { output[i] = 0.0; presence[i] = 0.0; }
                    return;
                }
                double path = 0.0;
                size_t first = start >= (size_t)period ? start - (size_t)period + 1 : 1;
                for (size_t j = first; j < start; j++) path += fabs(input[j] - input[j - 1]);
                for (size_t i = start; i < end; i++) {
                    if (i > 0) path += fabs(input[i] - input[i - 1]);
                    int defined = i >= (size_t)period && path != 0.0;
                    presence[i] = defined ? 1.0 : 0.0;
                    if (defined) {
                        double net = input[i] - input[i - (size_t)period];
                        double numerator = signed_percent ? net * 100.0 : fabs(net);
                        output[i] = numerator == 0.0 ? 0.0 : numerator / path;
                    } else output[i] = 0.0;
                    // Publish P changes before eviction; never accumulate P+1.
                    if (i >= (size_t)period) {
                        size_t expired = i - (size_t)period + 1;
                        path -= fabs(input[expired] - input[expired - 1]);
                    }
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "path_ratio"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
