#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Complete-window sums have no dependence on observations before this
    // replay window. Keep the original extended-exponent input rounding.
    private struct AccelerationKernel(RangeAccelerationBands.State state, BoundedOutput target) : IBoundedKernel<AccelerationKernel>
    {
        private int _index = target.Origin;
        public static AccelerationKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
            => new(new(period), target);

        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[6];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }
}
#endif
