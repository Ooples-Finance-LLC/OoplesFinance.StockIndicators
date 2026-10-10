#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;

namespace OoplesFinance.StockIndicators.Indicators;

// Reuses the published Tensors OpenCL runtime and typed FP64 buffers. Financial
// semantics stay here; no private backend access or duplicated native bindings.
internal sealed class TensorsGpuExecution
{
    private static readonly Lazy<(TensorsGpuExecution? Engine, string Reason)> Shared = new(Create);
    private readonly OpenClContext _context;
    private readonly object _gate = new();
    private readonly Dictionary<(int Period, bool Sma, bool Asin, bool FromMean, bool Presence), CompiledKernel> _kernels = new();
    // One reusable workspace, bounded to 40 MiB (host input + up to four FP64 buffers).
    // Published host arrays are never cached or pooled.
    private const int MaxRetainedCount = 1_048_576;
    private Workspace? _workspace;
    internal string DeviceName => _context.DeviceName;

    private TensorsGpuExecution(OpenClContext context) => _context = context;

    internal static bool TryGet(out TensorsGpuExecution? engine, out string reason)
    {
        (engine, reason) = Shared.Value;
        return engine is not null;
    }

    private static (TensorsGpuExecution?, string) Create()
    {
        OpenClContext? context = null;
        try
        {
            context = new OpenClContext(OpenClNative.ClDeviceType.Gpu);
            // CL_DEVICE_DOUBLE_FP_CONFIG: round-to-nearest, Inf/NaN, denormals
            // are required by the binary64 indicator contract.
            var error = OpenClNative.clGetDeviceInfo(context.Device,
                (OpenClNative.ClDeviceInfo)0x1032, (UIntPtr)8, out ulong flags, out _);
            if (error != OpenClNative.ClError.Success || (flags & 7) != 7)
            {
                context.Dispose();
                return (null, "The GPU lacks the required double-precision capabilities.");
            }
            return (new TensorsGpuExecution(context), "Double-precision OpenCL GPU available.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException
            or EntryPointNotFoundException or BadImageFormatException or NotSupportedException)
        {
            context?.Dispose();
            return (null, ex.Message);
        }
    }

    internal void Execute(FusedBarExecution plan, Bar[] source, OwnedBarHistory? history,
        bool fromMean, CancellationToken cancellation)
    {
        // Binding, reusable scratch and readback share the package's in-order queue.
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            bool retain = source.Length <= MaxRetainedCount;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && _workspace.Count != source.Length)
                {
                    _workspace.Dispose();
                    _workspace = null;
                }
                work = _workspace ??= new Workspace(_context, source.Length);
            }
            else work = new Workspace(_context, source.Length);
            try
            {
                bool devicePresence = plan.OwnCloses(source, history, work.Input, cancellation);
                var sma = plan.SmaValues;
                var asin = plan.AsinValues;
                var key = (plan.SmaLength, sma is not null, asin is not null, fromMean, devicePresence);
                if (!_kernels.TryGetValue(key, out var compiled))
                {
                    if (_kernels.Count >= 32)
                    {
                        foreach (var entry in _kernels.Values) entry.Dispose();
                        _kernels.Clear();
                    }
                    compiled = Compile(plan.SmaLength, sma is not null, asin is not null, fromMean, devicePresence);
                    _kernels.Add(key, compiled);
                }
                var deviceInput = work.DeviceInput;
                var deviceSma = sma is null ? null : work.Sma;
                var deviceAsin = asin is null ? null : work.Asin;
                var deviceFlags = devicePresence ? work.Presence : null;
                cancellation.ThrowIfCancellationRequested();
                deviceInput.CopyFromHost(work.Input);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, deviceInput.Handle);
                kernel.SetArg(1, deviceSma?.Handle ?? deviceInput.Handle);
                kernel.SetArg(2, deviceAsin?.Handle ?? deviceInput.Handle);
                kernel.SetArg(3, deviceFlags?.Handle ?? deviceInput.Handle);
                kernel.SetArg(4, source.Length);
                cancellation.ThrowIfCancellationRequested();
                int blockSize = plan.SmaLength > 0 ? 64 : 1;
                ulong workItems = ((ulong)source.Length + (ulong)blockSize - 1) / (ulong)blockSize;
                kernel.Enqueue(new[] { workItems });
                if (sma is not null) deviceSma!.CopyToHost(sma);
                if (asin is not null) deviceAsin!.CopyToHost(asin[0]);
                if (devicePresence) deviceFlags!.CopyToHost(asin![1]);
                cancellation.ThrowIfCancellationRequested();
            }
            catch
            {
                // Do not reuse potentially failed buffers/queue work after a driver error
                // or cancellation. OpenCL retains pending command references on release.
                if (retain) _workspace = null;
                work.Dispose();
                throw;
            }
            finally { if (!retain) work.Dispose(); }
        }
    }

    private sealed class Workspace(OpenClContext context, int count) : IDisposable
    {
        internal int Count => count;
        internal double[] Input { get; } = GC.AllocateUninitializedArray<double>(count);
        private OpenClBuffer<double>? _input, _sma, _asin, _presence;
        internal OpenClBuffer<double> DeviceInput => _input ??= new(context, count);
        internal OpenClBuffer<double> Sma => _sma ??= new(context, count);
        internal OpenClBuffer<double> Asin => _asin ??= new(context, count);
        internal OpenClBuffer<double> Presence => _presence ??= new(context, count);
        public void Dispose()
        {
            _input?.Dispose(); _sma?.Dispose(); _asin?.Dispose(); _presence?.Dispose();
        }
    }

    private CompiledKernel Compile(int period, bool publishSma, bool publishAsin, bool fromMean, bool devicePresence)
    {
        // One work item owns a short sequential block. This avoids a global
        // prefix array and its cancellation error. Each starting window and
        // recurrence are exact under the CPU-established grid certificate.
        var source = $$"""
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            #define PERIOD {{period}}
            #define BLOCK {{(period > 0 ? 64 : 1)}}
            __kernel void indicators(__global const double* close,
                __global double* sma, __global double* value, __global double* present, int count)
            {
                size_t start = get_global_id(0) * BLOCK;
                if (start >= (size_t)count) return;
                size_t end = min(start + (size_t)BLOCK, (size_t)count);
                double sum = 0.0;
                #if PERIOD > 1
                if (PERIOD <= count) {
                    size_t first = start >= PERIOD - 1 ? start - (PERIOD - 1) : 0;
                    for (size_t j = first; j < start; ++j) sum += close[j];
                }
                #endif
                for (size_t i = start; i < end; ++i) {
                    double mean = 0.0;
                    #if PERIOD == 1
                    mean = close[i];
                    #elif PERIOD > 1
                    if (PERIOD <= count) {
                        sum += close[i];
                        if (i > start && i >= PERIOD) sum -= close[i - PERIOD];
                        if (i >= PERIOD - 1) mean = sum / (double)PERIOD;
                    }
                    #endif
                    {{(publishSma ? "sma[i] = mean;" : "")}}
                    {{(publishAsin ? $"double x = {(fromMean ? "mean" : "close[i]")}; int defined = x >= -1.0 && x <= 1.0; value[i] = defined ? asin(x) : 0.0; {(devicePresence ? "present[i] = defined ? 1.0 : 0.0;" : "")}" : "")}}
                }
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build(); // No relaxed math or reduced-precision options.
            return new CompiledKernel(program, new OpenClKernel(_context, program, "indicators"));
        }
        catch { program.Dispose(); throw; }
    }

    private sealed class CompiledKernel(OpenClProgram program, OpenClKernel kernel) : IDisposable
    {
        internal OpenClKernel Kernel => kernel;
        public void Dispose() { kernel.Dispose(); program.Dispose(); }
    }
}
#endif
