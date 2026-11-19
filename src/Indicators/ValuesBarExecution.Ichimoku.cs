#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedIchimokuState : IIndicatorState, IDisposable
    {
        private readonly int _kind;
        private readonly Streaming.RollingWindowMax? _high, _tenkanHigh;
        private readonly Streaming.RollingWindowMin? _low, _tenkanLow;
        public GeneratedIchimokuState(int length, int count, int kind)
        {
            _kind = kind;
            if (kind == 4) return;
            var period = Math.Max(1, Math.Min(length, count));
            _high = new(period); _low = new(period);
            if (kind == 2)
            {
                var tenkanPeriod = Math.Max(1, Math.Min(9, count));
                _tenkanHigh = new(tenkanPeriod); _tenkanLow = new(tenkanPeriod);
            }
        }
        public double Update(in Bar bar)
        {
            // Chikou publishes the computing-bar close; plotting displacement
            // remains outside the numerical series, as in the existing state.
            if (_kind == 4) return bar.Close;
            var line = PriceMean.Of(_high!.Add(bar.High, out _), _low!.Add(bar.Low, out _));
            if (_kind != 2) return line;
            var tenkan = PriceMean.Of(_tenkanHigh!.Add(bar.High, out _), _tenkanLow!.Add(bar.Low, out _));
            return PriceMean.Of(tenkan, line);
        }
        public void Reset() { _high?.Reset(); _low?.Reset(); _tenkanHigh?.Reset(); _tenkanLow?.Reset(); }
        public void Dispose() { _high?.Dispose(); _low?.Dispose(); _tenkanHigh?.Dispose(); _tenkanLow?.Dispose(); }
    }
}
#endif
