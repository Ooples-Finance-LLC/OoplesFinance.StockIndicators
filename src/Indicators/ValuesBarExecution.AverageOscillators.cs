#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static int AverageOscillatorWindow(IIndicator indicator) => indicator switch
    {
        MovingAverageDifference { Exponential: false } difference => Math.Max(difference.FirstPeriod, difference.SecondPeriod),
        AwesomeWithDetails { Source: null } awesome => awesome.SlowPeriod,
        _ => 0
    };

    private static Bar FillAverageOscillator(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned) => indicator switch
    {
        MovingAverageDifference => FillBoundedWindow<AverageDifferenceKernel>(source, output, indicator, AverageOscillatorWindow(indicator), cancellation, owned),
        AwesomeWithDetails => FillBoundedWindow<AwesomeKernel>(source, output, indicator, AverageOscillatorWindow(indicator), cancellation, owned),
        _ => throw new InvalidOperationException("Unqualified average oscillator kernel.")
    };

    private struct AverageDifferenceKernel(MovingAverageDifference.State state, BoundedOutput target) : IBoundedKernel<AverageDifferenceKernel>
    {
        private int _index = target.Origin;
        public static AverageDifferenceKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var difference = (MovingAverageDifference)indicator;
            return new(new(difference.FirstPeriod, difference.SecondPeriod, false), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[2];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart) target.Values[1][index] = values[1];
            return values[0];
        }
    }

    private struct AwesomeKernel(AwesomeWithDetails.State state, BoundedOutput target) : IBoundedKernel<AwesomeKernel>
    {
        private int _index = target.Origin;
        public static AwesomeKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target)
        {
            var awesome = (AwesomeWithDetails)indicator;
            return new(new(awesome.FastPeriod, awesome.SlowPeriod, false), target);
        }
        public double Update(in Bar bar)
        {
            Span<double> values = stackalloc double[4];
            state.Update(in bar, values);
            int index = _index++;
            if (index >= target.WriteStart)
                for (int slot = 1; slot < values.Length; slot++) target.Values[slot][index] = values[slot];
            return values[0];
        }
    }
}
#endif
