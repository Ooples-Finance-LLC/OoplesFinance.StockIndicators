#if !NETFRAMEWORK
using AiDotNet.Tensors.Engines;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal sealed partial class TensorsGpuExecution
{
    // Thirty-five fixed keys; window/lag periods are arguments, never new compiled programs.
    private readonly Dictionary<int, CompiledKernel> _pointwiseKernels = new();

    internal Bar ExecutePointwise(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned, IIndicator? secondary = null)
    {
        if (ValuesBarExecution.IsGpuCandleWindow(indicator))
            return ExecuteCandleWindow(source, output[0], indicator, cancellation, owned);
        if (indicator is WindowVortex vortex)
            return ExecutePackedVortex(source, output, vortex, cancellation, owned);
        if (indicator is WindowUltimateOscillator ultimate)
            return ExecutePackedUltimate(source, output, ultimate, cancellation, owned);
        if (indicator is WindowMoneyFlowIndex moneyFlow)
            return ExecutePackedMoneyFlow(source, output, moneyFlow, cancellation, owned);
        var arithmetic = indicator as CandleArithmetic;
        var rounding = indicator as PriceRoundingTransform;
        var lagged = indicator as LaggedPriceChange;
        var rollingSum = indicator as RollingPriceSum;
        var pathRatio = indicator as WindowPathRatio;
        var affine = indicator as NormalizedConvolution;
        var regression = indicator as WindowLinearRegression;
        int weightedPeriod = indicator switch
        {
            Wma weighted => Math.Max(1, weighted.Length),
            FixedPeriodWma weighted => weighted.Period,
            TriangularWindowAverage weighted => weighted.Period,
            EndpointWeightedAverage weighted => weighted.Period,
            WindowLinearRegression { Output: not WindowRegressionOutput.Angle } fit => fit.Period,
            NormalizedConvolution { HasAffineKernel: true } convolution => convolution.Kernel.Count,
            _ => 0
        };
        var midpoint = indicator as WindowRangeMidpoint;
        var extreme = indicator as DecayingWindowExtreme;
        var index = indicator as WindowExtremeIndex;
        var aroon = indicator as WindowAroon;
        var priceRange = indicator as WindowPriceRange;
        var median = ValuesBarExecution.IsGpuPercentile(indicator) ? (RollingPercentile)indicator : null;
        int candleExtremePeriod = indicator is HighestHigh high ? high.Length : indicator is LowestLow low ? low.Length : 0;
        if (secondary is not null && (index is not { Maximum: false }
            || secondary is not WindowExtremeIndex { Maximum: true } maximum || maximum.Period != index.Period))
            throw new NotSupportedException("GPU index pairs require matching minimum and maximum periods.");
        bool trueRange = indicator is TrueRange;
        bool engulfing = indicator is EngulfingPattern;
        bool polarity = indicator is BullishCandle or BearishCandle;
        bool body = engulfing || polarity;
        if (source.Length == 0 || extreme is { Decay: not 0 }
            || arithmetic is null && rounding is null && midpoint is null && extreme is null && index is null && aroon is null && priceRange is null && median is null && candleExtremePeriod <= 0 && rollingSum is null && pathRatio is null && weightedPeriod == 0 && !trueRange && !body && !ValuesBarExecution.IsGpuLagged(indicator))
            throw new NotSupportedException("Unqualified GPU pointwise kernel.");
        int operation = lagged is not null ? lagged.Kind switch
        {
            PriceChangeKind.Difference => 8,
            PriceChangeKind.Gain => 9,
            PriceChangeKind.Loss => 10,
            PriceChangeKind.Ratio => 11,
            _ => throw new NotSupportedException("Unqualified GPU lagged operation.")
        } : pathRatio is not null ? 31 : weightedPeriod > 0 ? 30 : rollingSum is not null ? 29 : trueRange ? 28 : median is not null ? 27 : candleExtremePeriod >= source.Length ? indicator is HighestHigh ? 26 : 25
            : candleExtremePeriod > 0 ? indicator is HighestHigh ? 24 : 23 : priceRange is not null ? 22 : aroon is not null ? 21 : index is not null ? secondary is not null ? 20 : index.Maximum ? 19 : 18
            : extreme is not null ? extreme.Maximum ? 17 : 16
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
                if (_workspace is not null && (_workspace.Count != source.Length || _workspace.IsPackedBars))
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
                var right = trueRange || arithmetic is not null || body || midpoint is { CandleRanges: true } || aroon is not null || priceRange is not null ? work.RightInput : null;
                var lowColumn = trueRange ? work.LowInput : null;
                double[]? presence = null;
                Bar latest = default;
                // Positive weighted totals are bounded by P(P+1)/2. Signed endpoint
                // and regression products/totals fit 2P^2, including the shifted
                // lag sum before eviction. Keep the certificate bound in Int32.
                bool endpoint = indicator is EndpointWeightedAverage || regression is not null;
                int certifiedLimit = endpoint ? 32767 : 65535;
                int affineTerms = affine is null ? 0 : AffineGpuTerms(affine);
                // P absolute differences require 2P price terms; signed percent
                // publication requires 200. All-missing windows need no arithmetic.
                long pathTerms = pathRatio is null || pathRatio.Period >= source.Length ? 1
                    : Math.Max(2L * pathRatio.Period, pathRatio.Convention == PathRatioConvention.SignedPercent ? 200 : 2);
                int certifiedTerms = pathRatio is not null ? (int)Math.Min(int.MaxValue, pathTerms)
                    : affine is not null ? Math.Max(1, affineTerms)
                    : weightedPeriod == 0 ? rollingSum?.Period ?? 1
                    : weightedPeriod > certifiedLimit ? int.MaxValue
                    : endpoint ? (int)(2L * weightedPeriod * weightedPeriod)
                    : (int)((long)weightedPeriod * (weightedPeriod + 1) / 2);
                var sumProof = new Core.SmaCpuKernel.Certificate(certifiedTerms);
                bool sumCertified = pathRatio is not null ? pathTerms <= int.MaxValue : affine is not null ? affineTerms > 0 : weightedPeriod <= certifiedLimit;
                var medianProof = new Core.SmaCpuKernel.Certificate(Math.Max(1, (median?.Period ?? 1) - 1));
                bool medianCertified = true;
                var percentileProof = new Core.SmaCpuKernel.Certificate(4);
                bool percentileCertified = true;
                bool quarterPercentile = median is { Percentile: .25 or .75, Period: > 1 };
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    latest = source[i];
                    if (!ValuesBarExecution.AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
                    if (owned is not null) owned[i] = latest;
                    left[i] = candleExtremePeriod > 0 ? indicator is HighestHigh ? latest.High : latest.Low : aroon is not null ? latest.High : priceRange is not null ? latest.Low : midpoint is { CandleRanges: true } ? latest.Low
                        : body ? latest.Open : arithmetic is null ? latest.Close : Select(in latest, arithmetic.Left);
                    if (pathRatio is not null && pathRatio.Period < source.Length || weightedPeriod > 1 || rollingSum is { Period: > 1 } && rollingSum.Period <= source.Length)
                        sumCertified &= sumProof.Include(left[i]);
                    if (median is { AverageDuringWarmup: true } && i < median.Period - 1)
                        medianCertified &= medianProof.Include(left[i]);
                    if (quarterPercentile) percentileCertified &= percentileProof.Include(left[i]);
                    if (lowColumn is not null) lowColumn[i] = latest.Low;
                    if (right is not null) right[i] = trueRange ? latest.High : aroon is not null ? latest.Low : priceRange is not null ? latest.High : midpoint is not null ? latest.High
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
                if (!medianCertified) throw new NotSupportedException("Median startup inputs do not satisfy the exact GPU mean certificate.");
                if (!percentileCertified) throw new NotSupportedException("Percentile inputs do not satisfy the exact GPU interpolation certificate.");
                if (!sumCertified) throw new NotSupportedException(pathRatio is not null
                    ? "Path-ratio inputs do not satisfy the exact GPU difference/percentage certificate."
                    : weightedPeriod == 0
                    ? "Rolling sum inputs do not satisfy the exact GPU sum certificate."
                    : "Weighted-average period or inputs do not satisfy the exact GPU weighted-sum certificate.");
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
                if (lowColumn is not null) work.Sma.CopyFromHost(lowColumn);
                var kernel = compiled.Kernel;
                kernel.SetArg(0, deviceLeft.Handle);
                kernel.SetArg(1, deviceRight.Handle);
                kernel.SetArg(2, deviceOutput.Handle);
                kernel.SetArg(3, source.Length);
                kernel.SetArg(4, weightedPeriod > 0 ? weightedPeriod : pathRatio?.Period ?? rollingSum?.Period ?? lagged?.Period ?? midpoint?.Period ?? extreme?.Period ?? index?.Period ?? aroon?.Period ?? priceRange?.Period ?? median?.Period ?? candleExtremePeriod);
                if (weightedPeriod > 0)
                {
                    kernel.SetArg(5, regression is not null ? 6 + (int)regression.Output
                        : affine is not null ? 5 : indicator is EndpointWeightedAverage endAverage
                        ? endAverage.AverageDuringWarmup ? 4 : 3
                        : indicator is TriangularWindowAverage ? 2 : indicator is FixedPeriodWma ? 1 : 0);
                    kernel.SetArg(6, affine?.AffineFirstWeight ?? (indicator is EndpointWeightedAverage ? 2 * weightedPeriod - 1 : 0));
                    kernel.SetArg(7, affine?.AffineWeightStep ?? (indicator is EndpointWeightedAverage ? -3 : 0));
                }
                if (pathRatio is not null)
                {
                    kernel.SetArg(5, work.Right.Handle);
                    kernel.SetArg(6, pathRatio.Convention == PathRatioConvention.SignedPercent ? 1 : 0);
                }
                if (median is not null)
                {
                    kernel.SetArg(5, median.AverageDuringWarmup ? 1 : 0);
                    kernel.SetArg(6, (int)(median.Percentile * 4));
                }
                if (trueRange) kernel.SetArg(5, work.Sma.Handle);
                if (secondary is not null) kernel.SetArg(5, work.Right.Handle);
                if (aroon is not null)
                {
                    kernel.SetArg(5, work.Sma.Handle); kernel.SetArg(6, work.Presence.Handle);
                    kernel.SetArg(7, aroon.IncludeExtraBar ? 1 : 0); kernel.SetArg(8, aroon.OldestTie ? 1 : 0);
                }
                cancellation.ThrowIfCancellationRequested();
                if (operation is 25 or 26)
                {
                    // In-order queue: local prefixes and immutable block summaries finish
                    // before the second dispatch combines preceding blocks.
                    ulong blocks = ((ulong)source.Length + 255) / 256;
                    kernel.SetArg(5, work.Sma.Handle); kernel.SetArg(6, 0);
                    kernel.Enqueue(new[] { blocks });
                    cancellation.ThrowIfCancellationRequested();
                    kernel.SetArg(6, 1);
                    kernel.Enqueue(new[] { blocks });
                }
                else kernel.Enqueue(new[] { rollingSum is null && pathRatio is null && weightedPeriod == 0 ? (ulong)source.Length : ((ulong)source.Length + 63) / 64 });
                deviceOutput.CopyToHost(output[0]);
                if (pathRatio is not null) work.Right.CopyToHost(output[1]);
                if (secondary is not null) work.Right.CopyToHost(output[1]);
                if (aroon is not null)
                {
                    work.Sma.CopyToHost(output[1]); work.Presence.CopyToHost(output[2]);
                    // Presence depends only on the window length. Numeric outputs all come from the device.
                    long size = (long)aroon.Period + (aroon.IncludeExtraBar ? 1 : 0);
                    for (int i = 0; i < source.Length; i++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        output[3][i] = output[4][i] = output[5][i] = i + 1L >= size ? 1 : 0;
                    }
                }
                for (int i = 0; i < source.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!double.IsFinite(output[0][i])) IndicatorOutputPolicy.Validate(indicator, 0, i, output[0][i]);
                    if (secondary is not null && !double.IsFinite(output[1][i]))
                        IndicatorOutputPolicy.Validate(secondary, 0, i, output[1][i]);
                }
                if (output.Length > 1 && secondary is null && aroon is null && pathRatio is null)
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

    private static int AffineGpuTerms(NormalizedConvolution convolution)
    {
        int period = convolution.Kernel.Count;
        long first = convolution.AffineFirstWeight, step = convolution.AffineWeightStep;
        // Include the shifted oldest weight (lag P), and step*sum itself.
        // Division before multiplication also rejects bounds that exceed Int64.
        long coefficient = Math.Max(1, Math.Max(Math.Abs(step),
            Math.Max(Math.Abs(first), Math.Abs(first + step * period))));
        return coefficient > int.MaxValue / period ? 0 : (int)(coefficient * period);
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
        if (operation is 25 or 26) return CompilePrefixExtreme(operation == 26);
        if (operation == 27) return CompileMedian();
        if (operation == 28) return CompileTrueRange();
        if (operation == 29) return CompileRollingSum();
        if (operation == 30) return CompileWeightedAverage();
        if (operation == 31) return CompilePathRatio();
        if (operation == 32) return CompileVortex();
        if (operation == 33) return CompileUltimate();
        if (operation == 34) return CompileMoneyFlow();
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
            22 => "x == y ? 0.0 : y - x",
            16 or 17 or 23 or 24 => "x",
            18 or 19 or 20 => "(double)bestIndex",
            21 => "(double)(100L * ((long)period - ((long)i - highIndex))) / (double)period",
            _ => throw new NotSupportedException("Unqualified GPU pointwise operation.")
        };
        var previous = operation == 7
            ? "if (i < 2) { output[i] = 0.0; return; } double p = left[i - 1]; double q = right[i - 1];"
            : operation is >= 8 and <= 11 ? "if (i < (size_t)period) { output[i] = 0.0; return; } y = left[i - (size_t)period];"
            : operation is 14 or 15 or 22 ? """
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
            : operation is 16 or 17 or 23 or 24 ? $$"""
                size_t start = i + 1 > (size_t)period ? i + 1 - (size_t)period : 0;
                x = left[start];
                for (size_t j = start + 1; j <= i; j++)
                {
                    double next = left[j];
                    // Close extrema keep newest equal bits; candle extrema keep oldest.
                    if (next {{(operation == 24 ? ">" : operation == 23 ? "<" : operation == 17 ? ">=" : "<=")}} x) x = next;
                }
                """
            : operation is >= 18 and <= 20 ? IndexWindowSource(operation)
            : operation == 21 ? AroonWindowSource : "";
        var source = $$"""
            #pragma OPENCL EXTENSION cl_khr_fp64 : enable
            #pragma OPENCL FP_CONTRACT OFF
            __kernel void pointwise(__global const double* left, __global const double* right,
                __global double* output, int count, int period{{(operation == 20 ? ", __global double* secondary" : operation == 21 ? ", __global double* down, __global double* oscillator, int extra, int oldest" : "")}})
            {
                size_t i = get_global_id(0);
                if (i >= (size_t)count) return;
                double x = left[i];
                double y = right[i];
                {{previous}}
                output[i] = {{expression}};
                {{(operation == 20 ? "secondary[i] = (double)maxIndex;" : "")}}
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

    // One traversal finds both extreme indices. Integer numerators are exact (< 2^38),
    // and the single final division preserves the CPU percentage contract.
    private const string AroonWindowSource = """
        size_t size = (size_t)period + (size_t)extra;
        if (i + 1 < size) { output[i] = down[i] = oscillator[i] = 0.0; return; }
        size_t start = i + 1 - size;
        long highIndex = (long)start, lowIndex = (long)start;
        double high = left[start], low = right[start];
        for (size_t j = start + 1; j <= i; j++)
        {
            double h = left[j], l = right[j];
            if (h > high || (!oldest && h == high)) { high = h; highIndex = (long)j; }
            if (l < low || (!oldest && l == low)) { low = l; lowIndex = (long)j; }
        }
        down[i] = (double)(100L * ((long)period - ((long)i - lowIndex))) / (double)period;
        oscillator[i] = (double)(100L * (highIndex - lowIndex)) / (double)period;
        """;

    private static string IndexWindowSource(int operation)
    {
        // One dispatch and one window traversal for the two-output case.
        string comparison = operation == 19 ? ">=" : "<=";
        return $$"""
            size_t start = i + 1 > (size_t)period ? i + 1 - (size_t)period : 0;
            size_t bestIndex = start;
            x = left[start];
            {{(operation == 20 ? "size_t maxIndex = start; double high = x;" : "")}}
            for (size_t j = start + 1; j <= i; j++)
            {
                double next = left[j];
                if (next {{comparison}} x) { x = next; bestIndex = j; }
                {{(operation == 20 ? "if (next >= high) { high = next; maxIndex = j; }" : "")}}
            }
            """;
    }
}
#endif
