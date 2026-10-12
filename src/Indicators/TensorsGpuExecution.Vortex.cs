#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedVortex(Bar[] source, double[][] output, WindowVortex indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU Vortex input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Three host columns plus six device columns fit the 56 MiB budget.
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
                int count = source.Length;
                var packed = work.PackedHost;
                long terms = indicator.Period >= count ? 1 : 2L * indicator.Period;
                var proof = new Core.SmaCpuKernel.Certificate((int)Math.Min(int.MaxValue, terms));
                bool certified = terms <= int.MaxValue;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    packed[i] = latest.Close; packed[count + i] = latest.High; packed[2 * count + i] = latest.Low;
                    if (indicator.Period < count)
                        certified &= proof.Include(latest.High) & proof.Include(latest.Low) & proof.Include(latest.Close);
                }
                cancellation.ThrowIfCancellationRequested();
                if (!certified) throw new NotSupportedException("Vortex inputs do not satisfy the exact GPU interval-sum certificate.");
                if (!_pointwiseKernels.TryGetValue(32, out var compiled))
                {
                    compiled = CompileVortex(); _pointwiseKernels.Add(32, compiled);
                }
                var input = work.PackedInput; var result = work.PackedOutput;
                input.CopyFromHost(packed);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                kernel.SetArg(2, count); kernel.SetArg(3, indicator.Period);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { ((ulong)count + 63) / 64 });
                // The upload is blocking; its host storage is now safe to reuse.
                result.CopyToHost(packed);
                for (int slot = 0; slot < 3; slot++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    Array.Copy(packed, slot * count, output[slot], 0, count);
                }
                Array.Copy(output[2], output[3], count);
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
            __kernel void vortex(__global const double* input, __global double* result, int count, int period)
            {
                __global const double* close = input;
                __global const double* high = input + (size_t)count;
                __global const double* low = input + 2 * (size_t)count;
                __global double* output = result;
                __global double* negativeOutput = result + (size_t)count;
                __global double* presence = result + 2 * (size_t)count;
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
