#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    internal sealed class VariableSmaState(VariablePeriodClassicAverage owner, int count) : IMultiOutputState
    {
        // A selected SMA only needs two exact prefixes. Store at most the
        // maximum window, without allocating from an unobserved huge period.
        private readonly JurikDyadic[] _prefixes = new JurikDyadic[Math.Min(owner.MaximumPeriod, count)];
        private JurikDyadic _sum;
        private long _index;

        public void Reset()
        {
            Array.Clear(_prefixes);
            _sum = default;
            _index = 0;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            bool publish = _index >= owner.MaximumAverage.First;
            int period = owner.MinimumPeriod;
            if (publish)
            {
                double selection = owner.PeriodSelector(bar);
                if (!double.IsFinite(selection))
                    throw new ArgumentOutOfRangeException(nameof(owner.PeriodSelector), "The selected period must be finite.");
                period = selection <= owner.MinimumPeriod ? owner.MinimumPeriod
                    : selection >= owner.MaximumPeriod ? owner.MaximumPeriod : (int)selection;
            }
            // Validate the selection before mutating state, as the original does.
            output.Clear();
            _prefixes[(int)(_index % _prefixes.Length)] = _sum;
            _sum.AddExact(JurikDyadic.FromDouble(bar.Close));
            if (publish)
            {
                var window = _sum;
                window.Subtract(_prefixes[(int)((_index - period + 1) % _prefixes.Length)]);
                output[0] = window.Mean(period);
                output[1] = 1;
            }
            _index++;
        }
    }
}
#endif
