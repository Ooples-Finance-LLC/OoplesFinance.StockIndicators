#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedNetVolumeState : IIndicatorState
    {
        private bool _hasPrevious;
        private double _previous;
        public double Update(in Bar bar)
        {
            var result = !_hasPrevious ? 0 : bar.Close > _previous ? bar.Volume : bar.Close < _previous ? -bar.Volume : 0;
            _previous = bar.Close;
            _hasPrevious = true;
            return result;
        }
        public void Reset() { _previous = 0; _hasPrevious = false; }
    }

    private sealed class GeneratedRawRangeState : IIndicatorState
    {
        public double Update(in Bar bar) => bar.High - bar.Low;
        public void Reset() { }
    }

    private sealed class GeneratedMarketFacilitationState : IIndicatorState
    {
        public double Update(in Bar bar) => Helpers.RoundedRangeRatio.Of(bar.High, bar.Low, bar.Volume);
        public void Reset() { }
    }
}
#endif
