#if !NETFRAMEWORK
using AiDotNet.Tensors.NumericOperations;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Lazy initialization leaves other pointwise kernels' startup unchanged.
    private static class TensorRounding
    {
        internal static readonly DoubleOperations Operations = new();
    }

    private static PointwiseRegion ComputeTensorRoundingRegion(ReadOnlySpan<Bar> source,
        Span<double> values, OwnedBarBuffer? owned, int offset, bool ceiling, CancellationToken cancellation)
    {
        var region = new PointwiseRegion { InvalidOutputIndex = -1 };
        for (int i = 0; i < source.Length; i++)
        {
            if (cancellation.IsCancellationRequested) return region;
            var bar = source[i];
            region.Last = bar;
            if (!AllFieldsFinite(in bar))
            {
                region.InvalidInput = true;
                return region;
            }
            values[i] = bar.Close;
            if (owned is not null) owned[offset + i] = bar;
        }
        // The final output is also the input buffer: no intermediate column or
        // per-element delegate. The dependency supplies SIMD and scalar tails.
        // Directed rounding of finite binary64 values preserves finite results
        // and signed zero, so neither operation needs an overflow/presence pass.
        if (ceiling) TensorRounding.Operations.Ceiling(values, values);
        else TensorRounding.Operations.Floor(values, values);
        return region;
    }
}
#endif
