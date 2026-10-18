#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel CompilePrefixExtreme(bool maximum)
    {
        var source = $$"""
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void prefix_extreme(__global const double* input, __global const double* unused,
                __global double* output, int count, int period, __global double* summaries, int phase)
            {
                size_t block = get_global_id(0), start = block * 256;
                if (start >= (size_t)count) return;
                size_t end = min(start + 256, (size_t)count);
                if (phase == 0)
                {
                    double best = input[start];
                    output[start] = best;
                    for (size_t i = start + 1; i < end; i++)
                    {
                        double next = input[i];
                        if (next {{(maximum ? ">" : "<")}} best) best = next;
                        output[i] = best;
                    }
                    summaries[block] = best;
                    return;
                }
                if (block == 0) return;
                // Summaries are immutable in this phase; no cross-work-item output reads.
                double prior = summaries[0];
                for (size_t b = 1; b < block; b++)
                {
                    double next = summaries[b];
                    if (next {{(maximum ? ">" : "<")}} prior) prior = next;
                }
                for (size_t i = start; i < end; i++)
                {
                    // Earlier blocks win equality, preserving the oldest signed-zero bits.
                    if (prior {{(maximum ? ">=" : "<=")}} output[i]) output[i] = prior;
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "prefix_extreme"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
