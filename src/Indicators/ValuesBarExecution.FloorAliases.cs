#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedFloorAliasScalarState(int slot) : IIndicatorState, IDisposable
    {
        private readonly Streaming.DailyPivotLevels _daily = new(false, floor: true);
        public double Update(in Bar bar) => _daily.Next(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, true)[slot];
        public void Reset() => _daily.Reset();
        public void Dispose() { }
    }
    private sealed class GeneratedFloorAliasState : IMultiOutputState, IDisposable
    {
        private readonly Streaming.DailyPivotLevels _daily = new(false, floor: true);
        public void Update(in Bar bar, Span<double> outputs) =>
            _daily.Next(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, true).AsSpan().CopyTo(outputs);
        public void Reset() => _daily.Reset();
        public void Dispose() { }
    }
}
#endif
