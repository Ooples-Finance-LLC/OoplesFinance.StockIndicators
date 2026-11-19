#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    // Only the fourth and second pairs are published. Without custom components,
    // the other two pairs are independent and cannot affect these six outputs.
    private sealed class GeneratedFourOscillatorState : IMultiOutputState, IDisposable
    {
        private readonly ExponentialMovingAverageSmoother _first, _second, _third, _fourth, _signal1, _signal2;
        private readonly bool _percentage;
        private bool _invalid1, _invalid2;
        public GeneratedFourOscillatorState(int length1, int length2, int length3, int length4, bool percentage)
        {
            _first = new(Math.Max(1, length1)); _second = new(Math.Max(1, length2));
            _third = new(Math.Max(1, length3)); _fourth = new(Math.Max(1, length4));
            _signal1 = new(Math.Max(1, length1)); _signal2 = new(Math.Max(1, length1));
            _percentage = percentage;
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            var first = _first.Next(bar.Close, true); var second = _second.Next(bar.Close, true);
            var third = _third.Next(bar.Close, true); var fourth = _fourth.Next(bar.Close, true);
            var line1 = _percentage ? RoundedPercentageChange.Of(first, third) : first - third;
            var line2 = _percentage ? RoundedPercentageChange.Of(fourth, second) : fourth - second;
            _invalid1 |= !double.IsFinite(line1); _invalid2 |= !double.IsFinite(line2);
            var signal1 = _invalid1 ? double.NaN : _signal1.Next(line1, true);
            var signal2 = _invalid2 ? double.NaN : _signal2.Next(line2, true);
            outputs[0] = line1; outputs[1] = signal1; outputs[2] = line1 - signal1;
            outputs[3] = line2; outputs[4] = signal2; outputs[5] = line2 - signal2;
        }
        public void Reset()
        {
            _first.Reset(); _second.Reset(); _third.Reset(); _fourth.Reset(); _signal1.Reset(); _signal2.Reset();
            _invalid1 = _invalid2 = false;
        }
        public void Dispose()
        {
            _first.Dispose(); _second.Dispose(); _third.Dispose(); _fourth.Dispose(); _signal1.Dispose(); _signal2.Dispose();
        }
    }
}
#endif
