#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedElasticVolumeState : IIndicatorState, IDisposable
    {
        private readonly ElasticVolumeAverageWindow? _average;
        private readonly ElasticVolumeWindow? _volume;
        private readonly bool _unreachableAverage;
        internal GeneratedElasticVolumeState(int length, int count, int kind)
        {
            length = Math.Max(1, length);
            if (kind == 0)
            {
                // A simple volume average stays zero until its full period exists.
                _unreachableAverage = length > count;
                _average = new(MovingAvgType.SimpleMovingAverage, _unreachableAverage ? 1 : length, 20);
            }
            else
            {
                // No observation expires before the batch ends for a longer period.
                _volume = new(Math.Min(length, Math.Max(1, count)));
            }
        }
        public double Update(in Bar bar) => _volume is not null
            ? _volume.Next(bar.Close, bar.Volume, true)
            : _unreachableAverage
                ? _average!.NextWithAverage(bar.Close, bar.Volume, default, true)
                : _average!.Next(bar.Close, bar.Volume, true);
        public void Reset() { _average?.Reset(); _volume?.Reset(); }
        public void Dispose() { _average?.Dispose(); _volume?.Dispose(); }
    }
}
#endif
