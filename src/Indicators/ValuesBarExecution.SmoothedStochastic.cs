#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int SmoothedStochasticWindow(IIndicator indicator)
    {
        if (indicator is not WindowStochasticKdj { WilderSmoothing: false } owner) return 0;
        long window = (long)owner.Period + owner.KPeriod + owner.DPeriod - 2;
        return window <= int.MaxValue ? (int)window : 0;
    }

    private struct SmoothedStochasticKernel(WindowStochasticKdj.State state, BoundedOutput target) : IBoundedKernel<SmoothedStochasticKernel>
    {
        private int _index = target.Origin;
        public static SmoothedStochasticKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new((WindowStochasticKdj)indicator), target);
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
