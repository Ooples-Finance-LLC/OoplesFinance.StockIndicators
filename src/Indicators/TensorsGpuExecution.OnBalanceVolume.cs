#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedObv(Bar[] source, double[][] output, SeededOnBalanceVolume indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU OBV input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            bool retain = source.Length <= MaxRetainedCount * 7 / 9;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length || !_workspace.IsPackedBars || _workspace.PackedColumns != 3))
                {
                    _workspace.Dispose(); _workspace = null;
                }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true);
            }
            else work = new Workspace(_context, source.Length, packedBars: true);
            try
            {
                int count = source.Length;
                int period = indicator.AveragePeriod is int p && p < count ? p : 0;
                var summary = new Core.SmaCpuKernel.GridSummary();
                var packed = work.PackedHost;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    packed[i] = latest.Close; packed[count + i] = latest.Volume;
                    summary.Include(latest.Volume);
                }
                // Every prefix has at most N signed volumes. An average of P
                // prefixes contains at most N*P such terms. This also bounds
                // block totals, offset accumulation, and every partial mean sum.
                long terms = (long)count * Math.Max(1, period);
                cancellation.ThrowIfCancellationRequested();
                if (terms > int.MaxValue || !summary.Certifies((int)terms))
                    throw new NotSupportedException("OBV inputs do not satisfy the exact GPU prefix/average certificate.");
                cancellation.ThrowIfCancellationRequested();
                var input = work.PackedInput; var result = work.PackedOutput;
                input.CopyFromHost(packed);
                ulong blocks = ((ulong)count + 127) / 128;
                for (int stage = 0; stage < 3; stage++)
                {
                    int key = 35 + stage;
                    if (!_pointwiseKernels.TryGetValue(key, out var compiled))
                    {
                        compiled = CompileObv(stage); _pointwiseKernels.Add(key, compiled);
                    }
                    var kernel = compiled.Kernel;
                    kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                    kernel.SetArg(2, count); kernel.SetArg(3, period);
                    kernel.SetArg(4, indicator.Seed == ObvSeed.FirstVolume ? 1 : 0);
                    cancellation.ThrowIfCancellationRequested();
                    // The published context's in-order queue separates prefix,
                    // correction, and publication without host result computation.
                    kernel.Enqueue(new[] { blocks });
                }
                result.CopyToHost(packed);
                for (int slot = 0; slot < 3; slot++)
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

    private CompiledKernel CompileObv(int stage)
    {
        const string prefix = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int seed) {
                size_t block = get_global_id(0), start = block * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                double total = 0.0;
                for (size_t i = start; i < end; i++) {
                    double volume = input[(size_t)count + i];
                    double change = i == 0 ? (seed ? volume : 0.0)
                        : input[i] > input[i-1] ? volume : input[i] < input[i-1] ? -volume : 0.0;
                    total += change;
                    result[i] = total == 0.0 ? 0.0 : total;
                }
                result[(size_t)count + block] = total;
            }
            """;
        const string correction = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int seed) {
                size_t block = get_global_id(0), start = block * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                double offset = 0.0;
                for (size_t b = 0; b < block; b++) offset += result[(size_t)count + b];
                for (size_t i = start; i < end; i++) {
                    double value = result[i] + offset;
                    result[i] = value == 0.0 ? 0.0 : value;
                }
            }
            """;
        const string publication = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int seed) {
                size_t start = get_global_id(0) * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                for (size_t i = start; i < end; i++) {
                    result[(size_t)count + i] = 0.0;
                    result[2 * (size_t)count + i] = 0.0;
                }
                if (period <= 0 || end <= (size_t)period) return;
                size_t first = max(start, (size_t)period);
                double total = 0.0;
                for (size_t j = first - (size_t)period + 1; j <= first; j++) total += result[j];
                for (size_t i = first; i < end; i++) {
                    result[(size_t)count + i] = total == 0.0 ? 0.0 : total / (double)period;
                    result[2 * (size_t)count + i] = 1.0;
                    if (i + 1 < end) {
                        total -= result[i - (size_t)period + 1];
                        total += result[i + 1];
                    }
                }
            }
            """;
        var program = new OpenClProgram(_context, stage == 0 ? prefix : stage == 1 ? correction : publication);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "execute"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
