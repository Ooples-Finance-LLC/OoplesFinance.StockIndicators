#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    // Eighteen fixed keys; window/lag periods are arguments, never new compiled programs.
    private readonly Dictionary<int, CompiledKernel> _pointwiseKernels = new();

    internal Bar ExecutePointwise(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (ValuesBarExecution.IsGpuCandleWindow(indicator))
            return ExecuteCandleWindow(source, output[0], indicator, cancellation, owned);
        var arithmetic = indicator as CandleArithmetic;
        var rounding = indicator as PriceRoundingTransform;
        var lagged = indicator as LaggedPriceChange;
        var midpoint = indicator as WindowRangeMidpoint;
        var extreme = indicator as DecayingWindowExtreme;
        bool engulfing = indicator is EngulfingPattern;
        bool polarity = indicator is BullishCandle or BearishCandle;
        bool body = engulfing || polarity;
        if (source.Length == 0 || extreme is { Decay: not 0 }
            || arithmetic is null && rounding is null && midpoint is null && extreme is null && !body && !ValuesBarExecution.IsGpuLagged(indicator))
            throw new NotSupportedException("Unqualified GPU pointwise kernel.");
        int operation = lagged is not null ? lagged.Kind switch
        {
            PriceChangeKind.Difference => 8,
            PriceChangeKind.Gain => 9,
            PriceChangeKind.Loss => 10,
            PriceChangeKind.Ratio => 11,
            _ => throw new NotSupportedException("Unqualified GPU lagged operation.")
        } : extreme is not null ? extreme.Maximum ? 17 : 16
            : midpoint is not null ? midpoint.CandleRanges ? 15 : 14
            : polarity ? indicator is BullishCandle ? 12 : 13
            : engulfing ? 7 : arithmetic is not null ? (int)arithmetic.Operation : 4 + (int)rounding!.Operation;
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
                var right = arithmetic is not null || body || midpoint is { CandleRanges: true } ? work.RightInput : null;
                double[]? presence = null;
                Bar latest = default;
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    left[i] = midpoint is { CandleRanges: true } ? latest.Low
                        : body ? latest.Open : arithmetic is null ? latest.Close : Select(in latest, arithmetic.Left);
                    if (right is not null) right[i] = midpoint is not null ? latest.High
                        : body ? latest.Close : Select(in latest, arithmetic!.Right);
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
                kernel.SetArg(4, lagged?.Period ?? midpoint?.Period ?? extreme?.Period ?? 0);
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
                if (retain)
                {
                    _workspace = null;
                    work.Dispose();
                }
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
            8 => "x == y ? 0.0 : x - y",
            9 => "x <= y ? 0.0 : x - y",
            10 => "x >= y ? 0.0 : y - x",
            11 => "x == 0.0 || y == 0.0 ? 0.0 : x / y",
            12 => "y > x ? 1.0 : 0.0",
            13 => "y < x ? 1.0 : 0.0",
            // A finite sum can be halved with one final rounding: addition is
            // exact on the subnormal lattice whenever halving could underflow.
            // An overflowing sum has normal operands whose halves are exact.
            // Only an exact zero sum is canonicalized; underflow keeps its sign.
            14 or 15 => "total == 0.0 ? 0.0 : isinf(total) ? x * 0.5 + y * 0.5 : total * 0.5",
            16 or 17 => "x",
            _ => throw new NotSupportedException("Unqualified GPU pointwise operation.")
        };
        var previous = operation == 7
            ? "if (i < 2) { output[i] = 0.0; return; } double p = left[i - 1]; double q = right[i - 1];"
            : operation is >= 8 and <= 11 ? "if (i < (size_t)period) { output[i] = 0.0; return; } y = left[i - (size_t)period];"
            : operation is 14 or 15 ? """
                size_t start = i + 1 > (size_t)period ? i + 1 - (size_t)period : 0;
                x = left[start]; y = right[start];
                for (size_t j = start + 1; j <= i; j++)
                {
                    double low = left[j], high = right[j];
                    if (low < x) x = low;
                    if (high > y) y = high;
                }
                double total = x + y;
                """
            : operation is 16 or 17 ? $$"""
                size_t start = i + 1 > (size_t)period ? i + 1 - (size_t)period : 0;
                x = left[start];
                for (size_t j = start + 1; j <= i; j++)
                {
                    double next = left[j];
                    // Equal extrema select the newest bits, including signed zero.
                    if (next {{(operation == 17 ? ">=" : "<=")}} x) x = next;
                }
                """ : "";
        var source = $$"""
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void pointwise(__global const double* left, __global const double* right,
                __global double* output, int count, int period)
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
