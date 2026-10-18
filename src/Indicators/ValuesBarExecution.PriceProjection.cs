#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Reuse the evaluator's guarded arithmetic and operation order, while reading
    // the selected fields from one validated bar instead of materialized columns.
    private readonly struct MedianPriceKernel : IPointwiseKernel
    {
        public bool AlwaysDefined => true;
        public bool CanOverflow => false;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            return PriceMean.Of(bar.High, bar.Low);
        }
    }
    private readonly struct TypicalPriceKernel : IPointwiseKernel
    {
        public bool AlwaysDefined => true;
        public bool CanOverflow => false;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            return PriceMean.Of(bar.High, bar.Low, bar.Close);
        }
    }
    private readonly struct WeightedCloseKernel : IPointwiseKernel
    {
        public bool AlwaysDefined => true;
        public bool CanOverflow => false;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            return PriceMean.Of(bar.High, bar.Low, bar.Close, bar.Close);
        }
    }
    private readonly struct FullTypicalPriceKernel : IPointwiseKernel
    {
        public bool AlwaysDefined => true;
        public bool CanOverflow => false;
        public double Invoke(in Bar bar, out bool defined)
        {
            defined = true;
            return PriceMean.Of(bar.Open, bar.High, bar.Low, bar.Close);
        }
    }
}
#endif
