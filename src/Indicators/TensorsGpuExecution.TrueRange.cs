#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel CompileTrueRange()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void true_range(__global const double* close, __global const double* high,
                __global double* output, int count, int unused, __global const double* low)
            {
                size_t i = get_global_id(0);
                if (i >= (size_t)count) return;
                double range = high[i] - low[i];
                // The first candle publishes its signed range, even for inverted OHLC.
                if (i == 0) { output[i] = range; return; }
                double prior = close[i - 1];
                double value = fmax(range, fmax(fabs(high[i] - prior), fabs(low[i] - prior)));
                // .NET Max prefers positive zero. OpenCL fmax does not guarantee
                // the sign of equal zeros; the two absolute distances are nonnegative.
                output[i] = value == 0.0 ? 0.0 : value;
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "true_range"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
