using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

// Kwan's published variant integrates a delayed stochastic/RSI/momentum ratio.
internal sealed class KwanWindow : IDisposable
{
    private readonly int _length, _delay;
    private readonly MovingAvgType _kind;
    private readonly Queue<double> _prices = new();
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private readonly Queue<BigInteger> _ratios = new();
    private readonly Average? _gain, _loss;
    private readonly RsiState? _fallback;
    private long _count;
    private double _previousPrice, _previousRsi;
    private BigInteger _sum, _previousIncrement;
    internal KwanWindow(MovingAvgType kind, int length, int delay)
    {
        _kind = kind; _length = Math.Max(1, length); _delay = Math.Max(1, delay);
        if (StrengthWindow.Supports(kind)) { _gain = new(kind, _length); _loss = new(kind, _length); }
        else _fallback = new(kind, _length);
    }
    private double Strength(double price, bool final)
    {
        if (_fallback is not null) return _fallback.Next(price, final);
        var change = _count == 0 ? default : GainLossShare.Change(price, _previousPrice);
        var gain = _gain!.Next(change.Mantissa > 0 ? change : default, final);
        var loss = _loss!.Next(change.Mantissa < 0 ? change.Absolute : default, final);
        var numerator = new ExactMeanAccumulator(); gain.AddTo(ref numerator, 100);
        var total = new ExactMeanAccumulator(); gain.AddTo(ref total); loss.AddTo(ref total);
        return _count > 0 && _length > 1 && _kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod && price == _previousPrice
            ? _previousRsi : GainLossShare.Of(numerator, total, 100);
    }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool high, bool final)
    {
        var first = deque.First;
        while (first is not null && first.Value.Index <= _count - _length) first = first.Next;
        var result = first is null ? value : high ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is not null && deque.First.Value.Index <= _count - _length) deque.RemoveFirst();
            while (deque.Last is not null && (high ? deque.Last.Value.Value <= value : deque.Last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    internal (double Value, Signal Trade) Next(double high, double low, double price, bool final, double? externalStrength = null)
    {
        StreamingInputValidation.Finite(high, nameof(high)); StreamingInputValidation.Finite(low, nameof(low)); StreamingInputValidation.Finite(price, nameof(price));
        var strength = externalStrength ?? Strength(price, final);
        var hh = Extreme(_highs, high, true, final); var ll = Extreme(_lows, low, false, final);
        var ratio = BigInteger.Zero;
        if (_prices.Count == _length && price != 0 && _prices.Peek() != 0 && hh != ll)
        {
            // Cancel both percentage factors before taking the complete quotient.
            var numerator = (ExactVarianceWindow.Units(price) - ExactVarianceWindow.Units(ll)) * ExactVarianceWindow.Units(strength) * ExactVarianceWindow.Units(_prices.Peek());
            var denominator = (ExactVarianceWindow.Units(hh) - ExactVarianceWindow.Units(ll)) * ExactVarianceWindow.Units(price);
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            ratio = RocBankValue.RoundUnits(numerator, denominator);
        }
        var increment = _ratios.Count == _delay ? _ratios.Peek() : BigInteger.Zero;
        var sum = _sum + increment;
        var change = increment.CompareTo(_previousIncrement);
        var trade = increment.Sign > 0 && change > 0 ? Signal.StrongBuy : increment.Sign < 0 && change < 0 ? Signal.StrongSell
            : increment.Sign > 0 ? Signal.Buy : increment.Sign < 0 ? Signal.Sell : Signal.None;
        if (final)
        {
            if (_prices.Count == _length) _prices.Dequeue(); _prices.Enqueue(price);
            if (_ratios.Count == _delay) _ratios.Dequeue(); _ratios.Enqueue(ratio);
            _sum = sum; _previousIncrement = increment; _previousPrice = price; _previousRsi = strength; _count++;
        }
        return (ExactMeanAccumulator.UnitRatio(sum, _delay), trade);
    }
    internal void Reset() { _prices.Clear(); _ratios.Clear(); _highs.Clear(); _lows.Clear(); _sum = _previousIncrement = default; _count = 0; _previousPrice = _previousRsi = 0; _gain?.Reset(); _loss?.Reset(); _fallback?.Reset(); }
    public void Dispose() { _fallback?.Dispose(); Reset(); }
    private sealed class Average
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<StrengthValue> _history = new();
        private ExactMeanAccumulator _sum, _weighted; private StrengthValue _previous; private long _count;
        internal Average(MovingAvgType kind, int length) { _kind = kind; _length = length; }
        internal StrengthValue Next(StrengthValue value, bool final)
        {
            var sum = _sum; var weighted = _weighted; StrengthValue result;
            var window = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (window)
            {
                weighted.Subtract(sum); value.AddTo(ref weighted, _length);
                if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1); value.AddTo(ref sum);
                result = _kind == MovingAvgType.WeightedMovingAverage ? StrengthValue.Round(weighted, (long)_length * (_length + 1L) / 2)
                    : _count + 1 < _length ? default : StrengthValue.Round(sum, _length);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _length)
            { value.AddTo(ref sum); result = StrengthValue.Round(sum, _count + 1); }
            else
            {
                var next = new ExactMeanAccumulator(); _previous.AddTo(ref next, _length - 1L);
                var ema = _kind == MovingAvgType.ExponentialMovingAverage; value.AddTo(ref next, ema ? 2 : 1);
                result = StrengthValue.Round(next, ema ? _length + 1L : _length);
            }
            if (final) { if (window) { if (_history.Count == _length) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = default; _previous = default; _count = 0; }
    }
}
