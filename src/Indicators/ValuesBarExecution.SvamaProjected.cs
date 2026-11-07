#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedSvamaState : IIndicatorState
    {
        private readonly SvamaWindow _window = new();
        public double Update(in Bar bar) => _window.Next(bar.Close, bar.Volume, true).Line;
        public void Reset() => _window.Reset();
    }
    private sealed class GeneratedProjectedLevelsState(int length) : IMultiOutputState, IDisposable
    {
        private readonly ProjectedLevelsWindow _window = new(length);
        public void Update(in Bar bar, Span<double> outputs) => _window.Next(bar.High, bar.Low, true).AsSpan().CopyTo(outputs);
        public void Reset() => _window.Reset();
        public void Dispose() => _window.Dispose();
    }
}
#endif
