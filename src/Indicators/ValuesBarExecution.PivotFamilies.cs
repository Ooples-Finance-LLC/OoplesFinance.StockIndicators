#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedPivotFamiliesState(int kind) : IMultiOutputState, IDisposable
    {
        private readonly Streaming.DailyPivotLevels _daily = new(false,
            camarilla: kind == 0, fibonacci: kind == 1, demark: kind == 2, floor: kind == 3);
        public void Update(in Bar bar, Span<double> outputs)
        {
            var levels = _daily.Next(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, true);
            levels.AsSpan().CopyTo(outputs);
        }
        public void Reset() => _daily.Reset();
        public void Dispose() { }
    }
}
#endif
