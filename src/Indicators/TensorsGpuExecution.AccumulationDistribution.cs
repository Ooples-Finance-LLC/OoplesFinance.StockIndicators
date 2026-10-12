#if !NETFRAMEWORK
using System.Numerics;
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    private Bar ExecutePackedAdl(Bar[] source, double[][] output, AccumulationDistributionWithAverage indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        if (source.Length == 0) throw new NotSupportedException("Unqualified GPU detailed ADL input.");
        lock (_gate)
        {
            cancellation.ThrowIfCancellationRequested();
            // Five host and ten device columns stay within the shared 56 MiB cap.
            bool retain = source.Length <= MaxRetainedCount * 7 / 15;
            Workspace work;
            if (retain)
            {
                if (_workspace is not null && (_workspace.Count != source.Length
                    || !_workspace.IsPackedBars || _workspace.PackedColumns != 5))
                { _workspace.Dispose(); _workspace = null; }
                work = _workspace ??= new Workspace(_context, source.Length, packedBars: true, packedColumns: 5);
            }
            else work = new Workspace(_context, source.Length, packedBars: true, packedColumns: 5);
            try
            {
                int count = source.Length, priceGrid = int.MaxValue, volumeGrid = int.MaxValue;
                int period = indicator.AveragePeriod is int p && p <= count ? p : 0;
                double maximumPrice = 0, maximumVolume = 0;
                var packed = work.PackedHost;
                Bar latest = default;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    packed[i] = latest.High; packed[count + i] = latest.Low;
                    packed[2 * count + i] = latest.Close; packed[3 * count + i] = latest.Volume;
                    packed[4 * count + i] = 0;
                    ValuesBarExecution.IncludeIntegerGrid(latest.High, ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Low, ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Close, ref priceGrid, ref maximumPrice);
                    ValuesBarExecution.IncludeIntegerGrid(latest.Volume, ref volumeGrid, ref maximumVolume);
                }
                if (priceGrid == int.MaxValue) priceGrid = 0;
                if (volumeGrid == int.MaxValue) volumeGrid = 0;
                double priceBound = Math.ScaleB(maximumPrice, -priceGrid);
                double volumeBound = Math.ScaleB(maximumVolume, -volumeGrid);
                // Small common-grid integers make all price differences exact.
                // Products and all N*P signed flow terms are bounded below.
                if (!(priceBound <= (1L << 40) && volumeBound <= (1L << 40))) RejectAdl();
                int minimumRangeShift = int.MaxValue, maximumRangeShift = 0;
                for (int i = 0; i < count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    for (int slot = 0; slot < 3; slot++)
                        packed[slot * count + i] = Math.ScaleB(packed[slot * count + i], -priceGrid);
                    packed[3 * count + i] = Math.ScaleB(packed[3 * count + i], -volumeGrid);
                    double range = Math.Abs(packed[i] - packed[count + i]);
                    if (range == 0) continue;
                    ulong units = (ulong)range;
                    if (!BitOperations.IsPow2(units)) RejectAdl();
                    int shift = BitOperations.TrailingZeroCount(units);
                    minimumRangeShift = Math.Min(minimumRangeShift, shift);
                    maximumRangeShift = Math.Max(maximumRangeShift, shift);
                }
                if (minimumRangeShift == int.MaxValue) minimumRangeShift = 0;
                int flowGrid = volumeGrid - maximumRangeShift;
                UInt128 productBound = (UInt128)(ulong)priceBound * 4 * (ulong)volumeBound;
                ulong terms = (ulong)count * (uint)Math.Max(1, period);
                int spread = maximumRangeShift - minimumRangeShift;
                // Divide the limit instead of multiplying potentially wide bounds.
                // On grid volumeGrid-maxRangeShift, every prefix/mean partial sum
                // has at most 52 significant bits and normal binary64 magnitude.
                if (flowGrid is < -500 or > 500 || spread >= 53
                    || productBound > (((UInt128)1 << 52) >> spread) / terms) RejectAdl();
                var input = work.PackedInput; var result = work.PackedOutput;
                cancellation.ThrowIfCancellationRequested();
                input.CopyFromHost(packed);
                for (int stage = 0; stage < 3; stage++)
                {
                    int key = 38 + stage;
                    if (!_pointwiseKernels.TryGetValue(key, out var compiled))
                    { compiled = CompileAdl(stage); _pointwiseKernels.Add(key, compiled); }
                    var kernel = compiled.Kernel;
                    kernel.SetArg(0, input.Handle); kernel.SetArg(1, result.Handle);
                    kernel.SetArg(2, count); kernel.SetArg(3, period); kernel.SetArg(4, volumeGrid);
                    cancellation.ThrowIfCancellationRequested();
                    kernel.Enqueue(new[] { ((ulong)count + 127) / 128 });
                }
                result.CopyToHost(packed);
                for (int slot = 0; slot < 5; slot++)
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

    private static void RejectAdl() =>
        throw new NotSupportedException("Detailed ADL inputs do not satisfy the exact GPU flow/prefix/average certificate.");

    private CompiledKernel CompileAdl(int stage)
    {
        const string prefix = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int volumeGrid) {
                size_t block = get_global_id(0), start = block * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                double total = 0.0;
                for (size_t i = start; i < end; i++) {
                    double high = input[i], low = input[(size_t)count + i];
                    double close = input[2 * (size_t)count + i], volume = input[3 * (size_t)count + i];
                    double range = high - low, balance = (close - low) - (high - close);
                    double multiplier = range == 0.0 || balance == 0.0 ? 0.0 : balance / range;
                    double flow = range == 0.0 || balance == 0.0 || volume == 0.0
                        ? 0.0 : ldexp((balance * volume) / range, volumeGrid);
                    total += flow;
                    result[i] = multiplier;
                    result[(size_t)count + i] = flow;
                    result[2 * (size_t)count + i] = total == 0.0 ? 0.0 : total;
                }
                result[3 * (size_t)count + block] = total;
            }
            """;
        const string correction = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int volumeGrid) {
                size_t block = get_global_id(0), start = block * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                double offset = 0.0;
                for (size_t b = 0; b < block; b++) offset += result[3 * (size_t)count + b];
                for (size_t i = start; i < end; i++) {
                    double value = result[2 * (size_t)count + i] + offset;
                    result[2 * (size_t)count + i] = value == 0.0 ? 0.0 : value;
                }
            }
            """;
        const string publication = """
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void execute(__global const double* input, __global double* result,
                int count, int period, int volumeGrid) {
                size_t start = get_global_id(0) * 128;
                size_t end = min(start + (size_t)128, (size_t)count);
                for (size_t i = start; i < end; i++) {
                    result[3 * (size_t)count + i] = 0.0;
                    result[4 * (size_t)count + i] = 0.0;
                }
                if (period <= 0 || end < (size_t)period) return;
                size_t first = max(start, (size_t)period - 1);
                double total = 0.0;
                for (size_t j = first - (size_t)period + 1; j <= first; j++) total += result[2 * (size_t)count + j];
                for (size_t i = first; i < end; i++) {
                    result[3 * (size_t)count + i] = total == 0.0 ? 0.0 : total / (double)period;
                    result[4 * (size_t)count + i] = 1.0;
                    if (i + 1 < end) {
                        total -= result[2 * (size_t)count + i - (size_t)period + 1];
                        total += result[2 * (size_t)count + i + 1];
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
