#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedUltimate(Bar[] source, double[][] output, WindowUltimateOscillator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU Ultimate input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Same three host and six device columns as packed Vortex.
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
                int count = source.Length, grid = int.MaxValue;
                double maximum = 0;
                bool hasOutput = indicator.LongPeriod < count;
                var packed = work.PackedHost;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    packed[i] = latest.Close; packed[count + i] = latest.High; packed[2 * count + i] = latest.Low;
                    if (hasOutput)
                    {
                        ValuesBarExecution.IncludeIntegerGrid(latest.High, ref grid, ref maximum);
                        ValuesBarExecution.IncludeIntegerGrid(latest.Low, ref grid, ref maximum);
                        ValuesBarExecution.IncludeIntegerGrid(latest.Close, ref grid, ref maximum);
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if (grid == int.MaxValue) grid = 0;
                if (hasOutput)
                {
                    if (!Core.UltimateRatioCertificate.FitsDouble(Math.ScaleB(maximum, -grid), indicator.LongPeriod))
                        throw new NotSupportedException("Ultimate inputs do not satisfy the exact GPU rational certificate.");
                    for (int i = 0; i < packed.Length; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        packed[i] = Math.ScaleB(packed[i], -grid);
                    }
                }
                if (!_pointwiseKernels.TryGetValue(33, out var compiled))
                {
                    compiled = CompileUltimate(); _pointwiseKernels.Add(33, compiled);
                }
                var input = work.PackedInput; var result = work.PackedOutput;
                input.CopyFromHost(packed);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                kernel.SetArg(2, count); kernel.SetArg(3, indicator.ShortPeriod);
                kernel.SetArg(4, indicator.MiddlePeriod); kernel.SetArg(5, indicator.LongPeriod);
                kernel.SetArg(6, indicator.ZeroRangeContribution ? 1 : 0);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { ((ulong)count + 63) / 64 });
                // Upload is blocking; reuse its host storage for packed readback.
                result.CopyToHost(packed);
                for (int slot = 0; slot < 2; slot++)
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

    private CompiledKernel CompileUltimate()
    {
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            inline double pressure(__global const double* close, __global const double* low, size_t i) {
                return close[i] - fmin(low[i], close[i-1]);
            }
            inline double interval(__global const double* close, __global const double* high,
                __global const double* low, size_t i, int zero) {
                return zero ? fmax(high[i] - low[i], fmax(fabs(high[i] - close[i-1]), fabs(low[i] - close[i-1])))
                    : fmax(high[i], close[i-1]) - fmin(low[i], close[i-1]);
            }
            __kernel void ultimate(__global const double* input, __global double* result,
                int count, int shortPeriod, int middlePeriod, int longPeriod, int zero)
            {
                __global const double* close = input;
                __global const double* high = input + (size_t)count;
                __global const double* low = input + 2 * (size_t)count;
                __global double* output = result;
                __global double* presence = result + (size_t)count;
                size_t start = get_global_id(0) * 64;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)64, (size_t)count);
                // Initialize the unused third column too: the workspace is shared
                // with Vortex and every readback element must be defined.
                for (size_t i = start; i < end; i++) result[2 * (size_t)count + i] = 0.0;
                if (longPeriod >= count) {
                    for (size_t i = start; i < end; i++) { output[i] = 0.0; presence[i] = 0.0; }
                    return;
                }
                int periods[3] = { shortPeriod, middlePeriod, longPeriod };
                double pressures[3] = { 0.0, 0.0, 0.0 };
                double ranges[3] = { 0.0, 0.0, 0.0 };
                for (int k = 0; k < 3; k++) {
                    size_t first = start >= (size_t)periods[k] ? start - (size_t)periods[k] + 1 : 1;
                    for (size_t j = first; j < start; j++) {
                        pressures[k] += pressure(close, low, j);
                        ranges[k] += interval(close, high, low, j, zero);
                    }
                }
                for (size_t i = start; i < end; i++) {
                    if (i > 0) {
                        double p = pressure(close, low, i), r = interval(close, high, low, i, zero);
                        for (int k = 0; k < 3; k++) { pressures[k] += p; ranges[k] += r; }
                    }
                    int defined = i >= (size_t)longPeriod;
                    double numerator = 0.0, denominator = 1.0;
                    if (defined) for (int k = 0; k < 3; k++) {
                        if (ranges[k] == 0.0) {
                            if (!zero) { defined = 0; break; }
                            continue;
                        }
                        numerator = numerator * ranges[k] + (double)(4 >> k) * pressures[k] * denominator;
                        denominator *= ranges[k];
                    }
                    presence[i] = defined ? 1.0 : 0.0;
                    output[i] = defined && numerator != 0.0 ? (100.0 * numerator) / (7.0 * denominator) : 0.0;
                    for (int k = 0; k < 3; k++) if (i >= (size_t)periods[k]) {
                        size_t old = i - (size_t)periods[k] + 1;
                        pressures[k] -= pressure(close, low, old);
                        ranges[k] -= interval(close, high, low, old, zero);
                    }
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "ultimate"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
