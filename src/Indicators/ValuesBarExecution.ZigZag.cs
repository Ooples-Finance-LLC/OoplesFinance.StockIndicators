#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    // Repainting requires the complete path. Do not expose provisional per-bar
    // values or substitute the separate ZigZagSnapshot pivot convention.
    private sealed class GeneratedZigZagWriter(int count, double deviation)
    {
        private readonly double[] _high = new double[count], _low = new double[count];
        public void Append(in Bar bar, int index)
        {
            _high[index] = bar.High; _low[index] = bar.Low;
        }
        public void Complete(double[] output, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            ZigZagPath.Compute(_high, _low, output, deviation);
            cancellation.ThrowIfCancellationRequested();
        }
    }
}
#endif
