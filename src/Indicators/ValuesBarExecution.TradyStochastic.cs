#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int TradyStochasticWindow(IIndicator indicator)
    {
        long window = indicator switch
        {
            FiftySeedStochastic raw => raw.Period,
            StochasticSmaKdj kdj => (long)kdj.Period + kdj.KPeriod + kdj.DPeriod - 2,
            StochasticSmaDifference difference => (long)difference.Period + difference.KPeriod + difference.DPeriod - 2,
            WindowStochasticMomentum momentum => momentum.Period,
            _ => 0
        };
        return window <= int.MaxValue ? (int)window : 0;
    }

    private static Bar FillTradyStochastic(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned) => indicator switch
    {
        FiftySeedStochastic => FillBoundedWindow<RawStochasticKernel>(source, output, indicator, TradyStochasticWindow(indicator), cancellation, owned),
        StochasticSmaKdj => FillBoundedWindow<TradyKdjKernel>(source, output, indicator, TradyStochasticWindow(indicator), cancellation, owned),
        StochasticSmaDifference => FillBoundedWindow<TradyStochasticDifferenceKernel>(source, output, indicator, TradyStochasticWindow(indicator), cancellation, owned),
        WindowStochasticMomentum => FillBoundedWindow<StochasticMomentumKernel>(source, output, indicator, TradyStochasticWindow(indicator), cancellation, owned),
        _ => throw new InvalidOperationException("Unqualified stochastic kernel.")
    };

    private readonly struct RawStochasticKernel(FiftySeedStochastic.RawState state) : IBoundedKernel<RawStochasticKernel>
    {
        public static RawStochasticKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) => new(new(period));
        public double Update(in Bar bar) => state.Update(in bar);
    }

    private struct TradyKdjKernel(StochasticSmaKdj.State state, BoundedOutput target) : IBoundedKernel<TradyKdjKernel>
    {
        private int _index = target.Origin;
        public static TradyKdjKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var owner = (StochasticSmaKdj)indicator;
            return new(new(owner.Period, owner.KPeriod, owner.DPeriod), target);
        }
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

    private struct TradyStochasticDifferenceKernel(StochasticSmaDifference.State state, BoundedOutput target) : IBoundedKernel<TradyStochasticDifferenceKernel>
    {
        private int _index = target.Origin;
        public static TradyStochasticDifferenceKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var owner = (StochasticSmaDifference)indicator;
            return new(new(owner.Period, owner.KPeriod, owner.DPeriod), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }

    private struct StochasticMomentumKernel(WindowStochasticMomentum.State state, BoundedOutput target) : IBoundedKernel<StochasticMomentumKernel>
    {
        private int _index = target.Origin;
        public static StochasticMomentumKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var owner = (WindowStochasticMomentum)indicator;
            return new(new(owner.Period), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }
}
#endif
