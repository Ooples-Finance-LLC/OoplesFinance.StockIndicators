#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel CompileVortex()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            inline double interval(__global const double* close, __global const double* high,
                __global const double* low, size_t i) {
                return fmax(high[i] - low[i], fmax(fabs(high[i] - close[i-1]), fabs(low[i] - close[i-1])));
            }
            inline double movement(__global const double* high, __global const double* low, size_t i, int negative) {
                return negative ? fabs(low[i] - high[i-1]) : fabs(high[i] - low[i-1]);
            }
            __kernel void vortex(__global const double* close, __global const double* high,
                __global double* output, int count, int period, __global const double* low,
                __global double* presence, __global double* negativeOutput)
            {
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                if (period >= count) {
                    for (size_t i = start; i < end; i++) { output[i] = 0.0; negativeOutput[i] = 0.0; presence[i] = 0.0; }
                    return;
                }
                double range = 0.0, positive = 0.0, negative = 0.0;
                size_t first = start >= (size_t)period ? start - (size_t)period + 1 : 1;
                for (size_t j = first; j < start; j++) {
                    range += interval(close, high, low, j);
                    positive += movement(high, low, j, 0); negative += movement(high, low, j, 1);
                }
                for (size_t i = start; i < end; i++) {
                    if (i > 0) {
                        range += interval(close, high, low, i);
                        positive += movement(high, low, i, 0); negative += movement(high, low, i, 1);
                    }
                    int defined = i >= (size_t)period && range != 0.0;
                    presence[i] = defined ? 1.0 : 0.0;
                    output[i] = defined && positive != 0.0 ? positive / range : 0.0;
                    negativeOutput[i] = defined && negative != 0.0 ? negative / range : 0.0;
                    // Evict after publication, keeping at most P intervals.
                    if (i >= (size_t)period) {
                        size_t old = i - (size_t)period + 1;
                        range -= interval(close, high, low, old);
                        positive -= movement(high, low, old, 0); negative -= movement(high, low, old, 1);
                    }
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "vortex"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
