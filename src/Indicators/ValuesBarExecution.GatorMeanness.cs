#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private sealed class GeneratedGatorMeannessState : IMultiOutputState, IDisposable
    {
        private readonly MeannessWindow? _meanness;
        private readonly AlligatorLineWindow? _jaw, _teeth, _lips;
        public GeneratedGatorMeannessState(int length, bool meanness)
        {
            if (meanness) _meanness = new(MovingAvgType.EhlersNoiseEliminationTechnology, length);
            else
            {
                _jaw = new(MovingAvgType.WildersSmoothingMethod, length, 8);
                _teeth = new(MovingAvgType.WildersSmoothingMethod, 8, 5);
                _lips = new(MovingAvgType.WildersSmoothingMethod, 5, 3);
            }
        }
        public void Update(in Bar bar, Span<double> outputs)
        {
            if (_meanness is not null)
            {
                var point = _meanness.Next(bar.Close, true);
                outputs[0] = point.Line; outputs[1] = point.Smoothed;
            }
            else
            {
                var price = PriceMean.Of(bar.High, bar.Low);
                var jaw = _jaw!.Next(price, true);
                var teeth = _teeth!.Next(price, true);
                var lips = _lips!.Next(price, true);
                outputs[0] = Math.Abs(jaw - teeth);
                outputs[1] = -Math.Abs(teeth - lips);
            }
        }
        public void Reset() { _meanness?.Reset(); _jaw?.Reset(); _teeth?.Reset(); _lips?.Reset(); }
        public void Dispose() { _meanness?.Dispose(); _jaw?.Dispose(); _teeth?.Dispose(); _lips?.Dispose(); }
    }
}
#endif
