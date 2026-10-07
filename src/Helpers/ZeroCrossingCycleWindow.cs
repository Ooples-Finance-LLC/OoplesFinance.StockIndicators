using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ZeroCrossingCycleWindow
{
    private readonly ClampedBandPassWindow _band;
    private double _previousReal, _previousTrigger, _cycle;
    private long _counter;
    internal ZeroCrossingCycleWindow(int length, double bandwidth) => _band = new(length, bandwidth, 0);
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var point = _band.Next(price, commit); var counter = _counter == long.MaxValue ? long.MaxValue : _counter + 1;
        var cycle = Math.Max(_cycle, 6);
        if ((point.Value > 0 && _previousReal <= 0) || (point.Value < 0 && _previousReal >= 0))
        { cycle = Math.Min(1.25 * _cycle, Math.Max(.8 * _cycle, 2d * counter)); counter = 0; }
        var distance = point.Value - point.Signal; var previous = _previousReal - _previousTrigger;
        var signal = distance > 0 ? distance > previous ? Signal.StrongBuy : Signal.Buy : distance < 0 ? distance < previous ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (commit) { _counter = counter; _cycle = cycle; _previousReal = point.Value; _previousTrigger = point.Signal; } return (cycle, signal);
    }
    internal void Reset() { _band.Reset(); _previousReal = _previousTrigger = _cycle = 0; _counter = 0; }
}
