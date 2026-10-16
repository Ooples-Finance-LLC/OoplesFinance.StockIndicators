#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillArithmetic(Bar[] source, double[][] output, CandleArithmetic indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned) => indicator.Operation switch
    {
        CandleArithmeticOperation.Add => FillArithmetic<Add>(source, output, indicator, cancellation, owned),
        CandleArithmeticOperation.Subtract => FillArithmetic<Subtract>(source, output, indicator, cancellation, owned),
        CandleArithmeticOperation.Multiply => FillArithmetic<Multiply>(source, output, indicator, cancellation, owned),
        CandleArithmeticOperation.Divide => FillArithmetic<Divide>(source, output, indicator, cancellation, owned),
        _ => throw new InvalidOperationException("Unqualified arithmetic operation.")
    };

    private static Bar FillArithmetic<TMath>(Bar[] source, double[][] output, CandleArithmetic indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned) where TMath : struct, IBinaryMath =>
        FillPointwiseKernel(source, output, indicator, new BinaryKernel<TMath>(indicator.Left, indicator.Right),
            cancellation, owned);

    private interface IBinaryMath
    {
        double Invoke(double left, double right);
        bool CanBeUndefined { get; }
    }
    private readonly struct Add : IBinaryMath
    {
        public double Invoke(double left, double right) => left + right;
        public bool CanBeUndefined => false;
    }
    private readonly struct Subtract : IBinaryMath
    {
        public double Invoke(double left, double right) => left - right;
        public bool CanBeUndefined => false;
    }
    private readonly struct Multiply : IBinaryMath
    {
        public double Invoke(double left, double right) => left * right;
        public bool CanBeUndefined => false;
    }
    private readonly struct Divide : IBinaryMath
    {
        public double Invoke(double left, double right) => left / right;
        public bool CanBeUndefined => true;
    }

    // The operation is specialized once per batch. Keep the public field choices
    // inside the kernel; callers never prepare or retain separate price arrays.
    private readonly struct BinaryKernel<TMath>(CandlePriceField left, CandlePriceField right) : IPointwiseKernel
        where TMath : struct, IBinaryMath
    {
        public double Invoke(in Bar bar, out bool defined)
        {
            var a = Select(in bar, left);
            var b = Select(in bar, right);
            defined = !default(TMath).CanBeUndefined || Math.Abs(b) > 0;
            return defined ? default(TMath).Invoke(a, b) : 0;
        }
        public bool AlwaysDefined => !default(TMath).CanBeUndefined;
        public bool CanOverflow => true;
        private static double Select(in Bar bar, CandlePriceField field) => field switch
        {
            CandlePriceField.Open => bar.Open,
            CandlePriceField.High => bar.High,
            CandlePriceField.Low => bar.Low,
            _ => bar.Close
        };
    }
}
#endif
