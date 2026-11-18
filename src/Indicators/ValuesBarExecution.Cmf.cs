#if !NETFRAMEWORK
using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static PointwiseRegion ComputeCmfColumn(Bar[] source, double[][] output, OwnedBarBuffer? owned,
        int start, int end, MoneyFlowWithDetails indicator, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        CmfPublished[] seedValues = seed.IsEmpty ? [] : System.Buffers.ArrayPool<CmfPublished>.Shared.Rent(seed.Length);
        CmfGrid flowGrid = default, volumeGrid = default;
        bool eligible = true;
        int count = seed.Length + end - start;
        try
        {
            // Capture each input once. The existing flow column retains its
            // publication; the not-yet-published presence column retains volume.
            // No caller memory is reread after certification.
            for (int i = 0; i < count; i++)
            {
                if (cancellation.IsCancellationRequested) return region;
                int row = start + i - seed.Length;
                var bar = i < seed.Length ? seed[i]
                    : row >= end - tail.Length ? tail[row - (end - tail.Length)] : source[row];
                if (!AllFieldsFinite(in bar))
                { region.InvalidInput = true; region.Last = bar; return region; }
                PublishCmfFlow(in bar, out double multiplier, out double flow);
                if (i < seed.Length) seedValues[i] = new(multiplier, flow, bar.Volume);
                else
                {
                    region.Last = bar;
                    if (owned is not null) owned[row] = bar;
                    output[0][row] = multiplier; output[1][row] = flow; output[3][row] = bar.Volume;
                    if (!double.IsFinite(multiplier) && region.InvalidOutputIndex < 0)
                    { region.InvalidOutputIndex = row; region.InvalidOutputValue = multiplier; }
                }
                if (!double.IsFinite(multiplier) || !double.IsFinite(flow)) eligible = false;
                else { flowGrid.Include(flow); volumeGrid.Include(bar.Volume); }
            }
            if (!eligible || !flowGrid.Fits(indicator.Period) || !volumeGrid.Fits(indicator.Period))
            {
                var state = new CmfPublishedWindow(indicator.Period);
                Span<double> values = stackalloc double[4];
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    int row = start + i - seed.Length;
                    var value = i < seed.Length ? seedValues[i]
                        : new CmfPublished(output[0][row], output[1][row], output[3][row]);
                    // The original window consumes the exact same once-rounded
                    // flows, including skipped nonfinite publications and zeros.
                    state.Update(in value, values);
                    if (i < seed.Length) continue;
                    output[2][row] = values[2]; output[3][row] = values[3];
                }
                return region;
            }
            var history = System.Buffers.ArrayPool<CmfIntegerPair>.Shared.Rent(indicator.Period);
            try
            {
                Int128 flows = 0, volumes = 0;
                int cursor = 0;
                for (int i = 0; i < count; i++)
                {
                    if (cancellation.IsCancellationRequested) return region;
                    int row = start + i - seed.Length;
                    double flow = i < seed.Length ? seedValues[i].Flow : output[1][row];
                    double volume = i < seed.Length ? seedValues[i].Volume : output[3][row];
                    var current = new CmfIntegerPair(flowGrid.Coefficient(flow), volumeGrid.Coefficient(volume));
                    if (i >= indicator.Period)
                    { flows -= history[cursor].Flow; volumes -= history[cursor].Volume; }
                    history[cursor] = current;
                    if (++cursor == indicator.Period) cursor = 0;
                    flows += current.Flow; volumes += current.Volume;
                    if (i < seed.Length) continue;
                    bool defined = i >= indicator.Period - 1 && volumes != 0;
                    output[2][row] = defined ? PublishCmfRatio(flows, volumes, flowGrid.Scale, volumeGrid.Scale) : 0;
                    output[3][row] = defined ? 1 : 0;
                }
            }
            finally { System.Buffers.ArrayPool<CmfIntegerPair>.Shared.Return(history); }
            return region;
        }
        finally
        {
            if (!seed.IsEmpty) System.Buffers.ArrayPool<CmfPublished>.Shared.Return(seedValues);
        }
    }

    private static double PublishCmfRatio(Int128 flows, Int128 volumes, int flowScale, int volumeScale)
    {
        if (flows == 0) return 0;
        // The worker certificate excludes Int128.MinValue and proves both
        // exact sums fit. Retain signs and the separate binary grids.
        UInt128 top = (UInt128)(flows < 0 ? -flows : flows);
        UInt128 bottom = (UInt128)(volumes < 0 ? -volumes : volumes);
        if (CertifiedCompactRatio.TryDivide(top, bottom, flowScale - volumeScale, out double result))
            return (flows < 0) != (volumes < 0) ? -result : result;
        // Normal-only certificate rejection, including subnormal publication,
        // keeps the original exact scaled dyadic ratio and its signed zero.
        var numerator = JurikDyadic.FromInteger(flows);
        numerator.ScaleByPowerOfTwo(flowScale - 1074);
        var denominator = JurikDyadic.FromInteger(volumes);
        denominator.ScaleByPowerOfTwo(volumeScale - 1074);
        return numerator.Ratio(denominator);
    }

    private struct CmfGrid
    {
        private int _end;
        private bool _hasValues;
        internal int Scale;
        internal void Include(double value)
        {
            var factor = new ExactMeanAccumulator.ProductFactor(value);
            long coefficient = factor.Mantissa;
            if (coefficient == 0) return;
            ulong magnitude = (ulong)(coefficient < 0 ? -coefficient : coefficient);
            int end = factor.Scale + 64 - BitOperations.LeadingZeroCount(magnitude);
            if (!_hasValues) { Scale = factor.Scale; _end = end; _hasValues = true; }
            else { Scale = Math.Min(Scale, factor.Scale); _end = Math.Max(_end, end); }
        }
        internal readonly bool Fits(int period)
        {
            int countBits = period <= 1 ? 0 : 32 - BitOperations.LeadingZeroCount((uint)period - 1);
            // Each absolute coefficient is strictly below 2^width. Therefore
            // every P-term sum and each remove-then-add intermediate is below
            // 2^127. This needs no 64-bit coefficient restriction or products.
            return !_hasValues || _end - Scale + countBits <= 127;
        }
        internal readonly Int128 Coefficient(double value)
        {
            var factor = new ExactMeanAccumulator.ProductFactor(value);
            return factor.Mantissa == 0 ? 0 : (Int128)factor.Mantissa << (factor.Scale - Scale);
        }
    }

    // Deliberately local copies preserve CompactState.Update and its small-input
    // and streaming IL. Formula parity is checked against that original state.
    private static void PublishCmfFlow(in Bar bar, out double multiplier, out double publishedFlow)
    {
        multiplier = publishedFlow = 0;
        if (bar.High != bar.Low) // NOSONAR: S1244 - Only an exactly flat candle takes the zero-range branch.
        {
            double flow;
            if (!TryCmfOrdinaryFlow(in bar, out multiplier, out flow))
            {
                var range = JurikDyadic.FromDouble(bar.High);
                range.Add(-bar.Low);
                var balance = JurikDyadic.FromDouble(bar.Close);
                balance.Multiply(2);
                balance.Add(-bar.High);
                balance.Add(-bar.Low);
                multiplier = balance.Sign == 0 ? 0 : balance.Ratio(range);
                balance.Multiply(bar.Volume);
                flow = balance.Sign == 0 ? 0 : balance.Ratio(range);
            }
            // The original published flow canonicalizes underflowed zero;
            // multiplier and CMF retain their rounded underflow signs.
            publishedFlow = flow == 0 ? 0 : flow;
        }
    }

    private static bool TryCmfOrdinaryFlow(in Bar bar, out double multiplier, out double flow)
    {
        multiplier = flow = 0;
        if (!Binary64ArithmeticCertificate.TryDifference(bar.Close, bar.Low, out var buying)
            || !Binary64ArithmeticCertificate.TryDifference(bar.High, bar.Close, out var selling)
            || !Binary64ArithmeticCertificate.TryDifference(buying, selling, out var balance)
            || !Binary64ArithmeticCertificate.TryDifference(bar.High, bar.Low, out var range)) return false;
        double product;
        if (balance == 0 || bar.Volume == 0) product = 0;
        else if (!Binary64ArithmeticCertificate.TryProduct(balance, bar.Volume, out product)) return false;
        // Certificates establish exact operands for both independent divisions.
        // Do not multiply the already-rounded multiplier by volume.
        multiplier = balance == 0 ? 0 : balance / range;
        flow = product == 0 ? 0 : product / range;
        return true;
    }

    private sealed class CmfPublishedWindow(int period)
    {
        private readonly Queue<(double Flow, double Volume)> _window = new();
        private JurikDyadic _flows, _volumes;
        internal void Update(in CmfPublished value, Span<double> output)
        {
            output.Clear();
            if (double.IsInfinity(value.Multiplier) || double.IsInfinity(value.Flow)) return;
            if (_window.Count == period)
            {
                var old = _window.Dequeue();
                _flows.Add(-old.Flow); _volumes.Add(-old.Volume);
            }
            _window.Enqueue((value.Flow, value.Volume));
            _flows.Add(value.Flow); _volumes.Add(value.Volume);
            if (_window.Count < period || _volumes.Sign == 0) return;
            output[2] = _flows.Sign == 0 ? 0 : _flows.Ratio(_volumes);
            output[3] = 1;
        }
    }

    private readonly record struct CmfPublished(double Multiplier, double Flow, double Volume);
    private readonly record struct CmfIntegerPair(Int128 Flow, Int128 Volume);
}
#endif
