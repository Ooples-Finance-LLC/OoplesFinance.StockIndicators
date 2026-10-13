#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class JurikValueState(JurikAdaptive owner, int count) : IIndicatorState
    {
        // The facade already validates the complete input domain. Reserve only
        // observed history and leave published overflow to its output policy.
        private readonly JurikCpuKernel _kernel = new(owner.Period, owner.Phase,
            owner.VolatilityPeriod, Math.Max(1, count), rejectOverflow: false);

        public void Reset() => _kernel.Reset();

        public double Update(in Bar bar)
        {
            Span<double> value = stackalloc double[1];
            _kernel.Next(bar, value, true);
            return value[0];
        }
    }
}
#endif
