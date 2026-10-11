#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private CompiledKernel? _candleWindowKernel;
    // Four host columns plus the existing five device buffers: retain at most 56 MiB.
    private const int MaxRetainedCandleCount = MaxRetainedCount * 7 / 9;

    private Bar ExecuteCandleWindow(Bar[] source, double[] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        var (operation, period, shadows) = indicator switch
        {
            StrictHaramiCandle c => (0, 1, c.ContainedShadows),
            BullishHaramiPattern c => (1, c.Period, c.ContainedShadows),
            BearishHaramiPattern c => (2, c.Period, c.ContainedShadows),
            UpsideTasukiGapPattern c => (3, c.Period, false),
            DownsideTasukiGapPattern c => (4, c.Period, false),
            _ => throw new NotSupportedException("Unqualified GPU candle window.")
        };
        bool ranges = operation >= 3 || shadows;
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Use the lower cap for every candle call: a same-sized cached workspace
            // may already contain the extra columns from an earlier shadow/Tasuki call.
            bool retain = source.Length <= MaxRetainedCandleCount;
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
                var open = work.Input;
                var close = work.RightInput;
                var high = ranges ? work.HighInput : null;
                var low = ranges ? work.LowInput : null;
                Bar latest = default;
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    open[i] = latest.Open; close[i] = latest.Close;
                    if (ranges) { high![i] = latest.High; low![i] = latest.Low; }
                }
                cancellation.ThrowIfCancellationRequested();
                _candleWindowKernel ??= CompileCandleWindow();
                var deviceOpen = work.DeviceInput; var deviceClose = work.Right;
                // SMA/presence scratch is not live during candle execution.
                var deviceHigh = ranges ? work.Sma : deviceOpen;
                var deviceLow = ranges ? work.Presence : deviceOpen;
                var deviceOutput = work.Asin;
                deviceOpen.CopyFromHost(open); deviceClose.CopyFromHost(close);
                if (ranges) { deviceHigh.CopyFromHost(high!); deviceLow.CopyFromHost(low!); }
                var kernel = _candleWindowKernel.Kernel;
                kernel.SetArg(0, deviceOpen.Handle); kernel.SetArg(1, deviceClose.Handle);
                kernel.SetArg(2, deviceHigh.Handle); kernel.SetArg(3, deviceLow.Handle);
                kernel.SetArg(4, deviceOutput.Handle); kernel.SetArg(5, source.Length);
                kernel.SetArg(6, operation); kernel.SetArg(7, period); kernel.SetArg(8, shadows ? 1 : 0);
                cancellation.ThrowIfCancellationRequested();
                kernel.Enqueue(new[] { (ulong)source.Length });
                deviceOutput.CopyToHost(output);
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

    private CompiledKernel CompileCandleWindow()
    {
        // Only ordered FP64 comparisons: no rounded price arithmetic or relaxed math.
        // Operation, period and shadow policy are arguments, not compilation/cache keys.
        const string source = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void candle_window(__global const double* open, __global const double* close,
                __global const double* high, __global const double* low, __global double* output,
                int count, int operation, int period, int shadows)
            {
                size_t i = get_global_id(0);
                if (i >= (size_t)count) return;
                output[i] = 0.0;
                if (i < (operation >= 3 ? 2 : 1)) return;
                if (operation != 0 && i <= (size_t)period) return;
                double o = open[i], c = close[i], po = open[i-1], pc = close[i-1];
                if (operation < 3)
                {
                    int prior_bearish = pc < po, bearish = c < o;
                    if (prior_bearish == bearish) return;
                    if (!(prior_bearish ? po > c && pc < o : po < c && pc > o)) return;
                    if (shadows && !(high[i-1] > high[i] && low[i-1] < low[i])) return;
                    if (operation == 1 && !(c > o)) return;
                    if (operation == 2 && !bearish) return;
                }
                else if (operation == 3)
                {
                    if (!(close[i-2] > open[i-2] && pc > po && c < o && high[i-2] < low[i-1]
                        && o > po && o < pc && c < po)) return;
                }
                else
                {
                    if (!(close[i-2] < open[i-2] && pc < po && c > o && low[i-2] > high[i-1]
                        && c < low[i-2] && c > high[i-1])) return;
                }
                if (operation != 0)
                {
                    for (size_t j = i - (size_t)period; j < i; j++)
                    {
                        if (operation == 1 && !(open[j] < open[j-1] && close[j] < close[j-1])) return;
                        if (operation == 2 && !(open[j] > open[j-1] && close[j] > close[j-1])) return;
                        if (operation == 3 && !(high[j] > high[j-1] && low[j] > low[j-1])) return;
                        if (operation == 4 && !(high[j] < high[j-1] && low[j] < low[j-1])) return;
                    }
                }
                output[i] = 1.0;
            }
            """;
        var program = new OpenClProgram(_context, source);
        try
        {
            program.Build();
            return new CompiledKernel(program, new OpenClKernel(_context, program, "candle_window"));
        }
        catch { program.Dispose(); throw; }
    }
}
#endif
