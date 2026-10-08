using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Reusable CPU computation with caller-owned, row-major output buffers.</summary>
/// <remarks>Instances belong to one consumer and are not thread safe. Update commits
/// one observation; Preview never consumes it. Process continues existing state;
/// call Reset for a fresh batch. Undefined outputs are NaN. Invalid inputs or short
/// buffers are rejected before processing. A numerical overflow may leave an earlier
/// prefix of a batch committed. Exact wide arithmetic may allocate; no approximate
/// numerical mode is selected implicitly.</remarks>
public abstract class IndicatorKernel
{
    /// <summary>Number of output slots for each observation.</summary>
    public abstract int OutputCount { get; }
    /// <summary>Clears observations while retaining reserved storage.</summary>
    public abstract void Reset();
    /// <summary>Computes and commits one observation.</summary>
    public void Update(in Bar bar, Span<double> output) => Next(bar, output, true);
    /// <summary>Computes the prospective next observation without consuming it.</summary>
    public void Preview(in Bar bar, Span<double> output) => Next(bar, output, false);
    private void Next(in Bar bar, Span<double> output, bool commit)
    {
        if (output.Length < OutputCount) throw new ArgumentException("Output buffer is too short.", nameof(output));
        Validate(bar);
        Evaluate(bar, output.Slice(0, OutputCount), commit);
    }
    /// <summary>Processes a batch into consecutive rows, continuing the current state.</summary>
    public void Process(ReadOnlySpan<Bar> bars, Span<double> output)
    {
        if ((long)bars.Length * OutputCount > output.Length)
            throw new ArgumentException("Output buffer is too short.", nameof(output));
        foreach (ref readonly var bar in bars) Validate(bar);
        for (var i = 0; i < bars.Length; i++) Evaluate(bars[i], output.Slice(i * OutputCount, OutputCount), true);
    }
    private static void Validate(in Bar bar)
    {
        if (!FrameworkCompatibility.IsFinite(bar.Open) || !FrameworkCompatibility.IsFinite(bar.High)
            || !FrameworkCompatibility.IsFinite(bar.Low) || !FrameworkCompatibility.IsFinite(bar.Close)
            || !FrameworkCompatibility.IsFinite(bar.Volume))
            throw new ArgumentOutOfRangeException(nameof(bar), "Kernel inputs must be finite.");
    }
    private protected abstract void Evaluate(in Bar bar, Span<double> output, bool commit);
}

/// <summary>CPU kernels sharing exact numerical stages with their indicator counterparts.</summary>
public static class IndicatorKernels
{
    /// <summary>Arcsine of close in radians; values outside [-1,1] are absent (NaN).</summary>
    public static IndicatorKernel Asin() => new AsinKernel();
    /// <summary>Writes the IEEE arcsine of each close in radians into caller-owned storage.</summary>
    /// <remarks>Values outside [-1,1], infinities, and NaN produce NaN, matching
    /// Math.Asin. Short outputs and partially overlapping buffers are rejected before writes.
    /// Exact in-place operation is supported; extra output slots are untouched.</remarks>
    public static void Asin(ReadOnlySpan<double> closes, Span<double> output)
    {
        if (output.Length < closes.Length)
            throw new ArgumentException("Output buffer is too short.", nameof(output));
        if (closes.Overlaps(output, out var offset) && offset != 0)
            throw new ArgumentException("Buffers must be disjoint or start at the same element.", nameof(output));
        for (var i = 0; i < closes.Length; i++) output[i] = Math.Asin(closes[i]);
    }
    /// <summary>Unsmoothed true range divided by a positive divisor.</summary>
    public static IndicatorKernel ScaledTrueRange(int divisor = 14)
    {
        _ = new ScaledTrueRange(divisor);
        return new ScalarKernel(new ScaledTrueRange.State(divisor, true));
    }
    /// <summary>Rickshaw Man, returning 100 or zero. Reserves both prior-range windows.</summary>
    public static IndicatorKernel RickshawMan(int dojiPeriod = 10, int nearPeriod = 5)
    {
        _ = new RickshawManCandle(dojiPeriod, nearPeriod);
#if NETFRAMEWORK
        return new ScalarKernel(new RickshawManCandle.State(dojiPeriod, nearPeriod, true));
#else
        return new ScalarKernel(new RickshawGridState(dojiPeriod, nearPeriod));
#endif
    }
    /// <summary>Bullish short body, returning one or zero. Reserves the percentile window.</summary>
    public static IndicatorKernel BullishShortBody(int period = 20, decimal percentile = .25m)
    {
        _ = new BullishShortBodyCandle(period, percentile);
        return new ScalarKernel(new PercentileCandleState(period, percentile, CandleLengthKind.Body, false, 1, true));
    }
    /// <summary>Exact simple moving average; zero until a complete window is available.</summary>
    public static IndicatorKernel Sma(int period = 20)
    {
        if (period < 1) throw new ArgumentOutOfRangeException(nameof(period));
        return new SmaKernel(period);
    }

    /// <summary>Jurik adaptive recurrence with its exact per-stage rounding and reserved histories.</summary>
    public static IndicatorKernel Jurik(int period = 20, double phase = 0, int volatilityPeriod = 10)
    {
        if (period < 1 || volatilityPeriod < 1 || !FrameworkCompatibility.IsFinite(phase))
            throw new ArgumentOutOfRangeException(nameof(period));
        return new JurikCpuKernel(period, phase, volatilityPeriod, Math.Max(period, volatilityPeriod));
    }
    /// <summary>Rolling pivots in PP,S1,S2,S3,S4,R1,R2,R3,R4 order; absent levels are NaN.</summary>
    public static IndicatorKernel RollingPivots(int period = 20, int offset = 0, PivotLevelStyle style = PivotLevelStyle.Standard)
    {
        if (period < 1 || offset < 0 || (long)period + offset > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (style is < PivotLevelStyle.Standard or > PivotLevelStyle.Woodie)
            throw new ArgumentOutOfRangeException(nameof(style));
        return new PivotCpuKernel(period, offset, style);
    }
    /// <summary>Bear and bull fractals confirmed for the observation rightSpan bars ago.
    /// Outputs belong to that earlier center, not the current observation; absent values are NaN.</summary>
    public static IndicatorKernel Fractal(int leftSpan = 2, int rightSpan = 2, bool useClose = false)
    {
        if (leftSpan < 2 || rightSpan < 2 || (long)leftSpan + rightSpan + 1 > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(leftSpan));
        return new FractalCpuKernel(leftSpan, rightSpan, useClose);
    }

    private sealed class ScalarKernel(IPreviewIndicatorState state) : IndicatorKernel
    {
        public override int OutputCount => 1;
        public override void Reset() => state.Reset();
        private protected override void Evaluate(in Bar bar, Span<double> output, bool commit) => output[0] = state.Update(bar, commit);
    }
    private sealed class AsinKernel : IndicatorKernel
    {
        public override int OutputCount => 1;
        public override void Reset() { }
        private protected override void Evaluate(in Bar bar, Span<double> output, bool commit) => output[0] = Math.Asin(bar.Close);
    }
    private sealed class SmaKernel(int period) : IndicatorKernel
    {
        private readonly double[] _window = new double[period];
        private int _position, _count;
        private ExactMeanAccumulator _sum;
        public override int OutputCount => 1;
        public override void Reset() { _sum = default; _position = _count = 0; }
        private protected override void Evaluate(in Bar bar, Span<double> output, bool commit)
        {
            var sum = _sum;
            if (_count == period) sum.Add(_window[_position], -1);
            sum.Add(bar.Close);
            var count = _count < period ? _count + 1 : period;
            var value = count == period ? sum.Mean(period) : 0;
            if (commit)
            {
                _sum = sum; _count = count; _window[_position] = bar.Close;
                _position = (_position + 1) % period;
            }
            output[0] = value;
        }
    }
}
