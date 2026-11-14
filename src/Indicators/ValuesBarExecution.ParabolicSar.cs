#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedParabolicSarState(double start, double increment, double maximum) : IIndicatorState, IDisposable
    {
        private readonly Streaming.ParabolicSarKernel _window = new(start, increment, maximum);
        private bool _started;
        private double _previousClose;
        public double Update(in Bar bar)
        {
            // ComputeParabolicSarFast always applies CustomRange, even for the
            // original close. Preserve its tolerance and first-bar seed.
            var previous = _started ? _previousClose : bar.Close;
            var inside = CalculationsHelper.IsWithinBarRange(bar.Close, bar.Low, bar.High);
            var high = inside ? bar.High : Math.Max(previous, bar.Close);
            var low = inside ? bar.Low : Math.Min(previous, bar.Close);
            var value = _window.Next(high, low, true);
            _previousClose = bar.Close; _started = true;
            return value;
        }
        public void Reset() { _window.Reset(); _started = false; _previousClose = 0; }
        public void Dispose() { }
    }
}
#endif
