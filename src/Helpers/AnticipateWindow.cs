using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class AnticipateWindow : IDisposable
{
    internal const int MaximumLength = 4096;
    internal static int ValidateLength(int length)
    {
        if (length > MaximumLength) throw new ArgumentOutOfRangeException(nameof(length), length, "Anticipate period must not exceed 4096.");
        return Math.Max(1, length);
    }
    private readonly int _length;
    private readonly ImpulseResponseWindow _impulse;
    private readonly AnticipateExactPhase _phase;
    private readonly Queue<BigInteger> _history = new();
    private double _previous;
    internal AnticipateWindow(MovingAvgType kind, int length, double bandwidth)
    { _length = ValidateLength(length); _impulse = new(kind, _length, bandwidth); _phase = new(_length); }
    internal (double Value, Signal Signal) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price));
        _impulse.Next(price, false, true); var filtered = _impulse.ExtendedOutput;
        var units = ExactVarianceWindow.Units(filtered.Mantissa) << filtered.UpperShift;
        var history = new[] { units }.Concat(_history.Reverse()).Take(_length).ToArray();
        var value = _phase.Predict(history);
        var signal = value > 0 ? value > _previous ? Signal.StrongBuy : Signal.Buy
            : value < 0 ? value < _previous ? Signal.StrongSell : Signal.Sell : Signal.None;
        if (final)
        {
            _impulse.Next(price, true, true);
            if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(units); _previous = value;
        }
        return (value, signal);
    }
    internal void Reset() { _impulse.Reset(); _history.Clear(); _previous = 0; }
    public void Dispose() { _impulse.Dispose(); _history.Clear(); }
}
