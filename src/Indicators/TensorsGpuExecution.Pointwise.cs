#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    // Eight keys: four arithmetic, three rounding and one adjacent-candle kernel.
    private readonly Dictionary<int, CompiledKernel> _pointwiseKernels = new();

    internal Bar ExecutePointwise(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        var arithmetic = indicator as CandleArithmetic;
        var rounding = indicator as PriceRoundingTransform;
        bool engulfing = indicator is EngulfingPattern;
        if (source.Length == 0 || arithmetic is null && rounding is null && !engulfing)
            throw new NotSupportedException("Unqualified GPU pointwise kernel.");
        int operation = engulfing ? 7 : arithmetic is not null ? (int)arithmetic.Operation : 4 + (int)rounding!.Operation;
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
                var left = work.Input;
                var right = arithmetic is not null || engulfing ? work.RightInput : null;
                double[]? presence = null;
                Bar latest = default;
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    left[i] = engulfing ? latest.Open : arithmetic is null ? latest.Close : Select(in latest, arithmetic.Left);
                    if (right is not null) right[i] = engulfing ? latest.Close : Select(in latest, arithmetic!.Right);
                    bool defined = operation != 3 || Math.Abs(right![i]) > 0;
                    if (operation == 6) defined = left[i] >= 0;
                    if (!defined)
                    {
                        if (presence is null)
                        {
                            presence = GC.AllocateUninitializedArray<double>(source.Length);
                            presence.AsSpan().Fill(1);
                        }
                        presence[i] = 0;
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if (!_pointwiseKernels.TryGetValue(operation, out var compiled))
                {
                    compiled = CompilePointwise(operation);
                    _pointwiseKernels.Add(operation, compiled);
                }
                var deviceLeft = work.DeviceInput;
                var deviceRight = right is null ? deviceLeft : work.Right;
                var deviceOutput = work.Asin;
                deviceLeft.CopyFromHost(left);
                if (right is not null) deviceRight.CopyFromHost(right);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, deviceLeft.Handle);
                kernel.SetArg(1, deviceRight.Handle);
                kernel.SetArg(2, deviceOutput.Handle);
                kernel.SetArg(3, source.Length);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { (ulong)source.Length });
                deviceOutput.CopyToHost(output[0]);
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!double.IsFinite(output[0][i])) IndicatorOutputPolicy.Validate(indicator, 0, i, output[0][i]);
                }
                if (output.Length > 1)
                    output[1] = presence ?? ValuesBarExecution.CompletePointwisePresence(source.Length, null, false, cancellation);
                cancellation.ThrowIfCancellationRequested();
                return latest;
            }
            catch
            {
                if (retain) _workspace = null;
                work.Dispose();
                throw;
            }
            finally { if (!retain) work.Dispose(); }
        }
    }

    private static double Select(in Bar bar, CandlePriceField field) => field switch
    {
        CandlePriceField.Open => bar.Open,
        CandlePriceField.High => bar.High,
        CandlePriceField.Low => bar.Low,
        _ => bar.Close
    };

    private CompiledKernel CompilePointwise(int operation)
    {
        // OpenCL C table-ulp-double requires correctly rounded FP64 arithmetic,
        // ceil, floor and sqrt. Never enable relaxed math or flush denormals.
        var expression = operation switch
        {
            0 => "x + y",
            1 => "x - y",
            2 => "x * y",
            3 => "y != 0.0 ? x / y : 0.0",
            4 => "ceil(x)",
            5 => "floor(x)",
            6 => "x >= 0.0 ? sqrt(x) : 0.0",
            7 => "y >= x && q < p && x <= q && y >= p && (x < q || y > p) ? 100.0 : " +
                 "y < x && q >= p && x >= q && y <= p && (x > q || y < p) ? -100.0 : 0.0",
            _ => throw new NotSupportedException("Unqualified GPU pointwise operation.")
        };
        var previous = operation == 7
            ? "if (i < 2) { output[i] = 0.0; return; } double p = left[i - 1]; double q = right[i - 1];"
            : "";
        var source = $$"""
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void pointwise(__global const double* left, __global const double* right,
                __global double* output, int count)
            {
                size_t i = get_global_id(0);
                if (i >= (size_t)count) return;
                double x = left[i];
                double y = right[i];
                {{previous}}
                output[i] = {{expression}};
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "pointwise"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
