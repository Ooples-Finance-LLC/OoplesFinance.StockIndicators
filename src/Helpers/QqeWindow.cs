using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Builder.Compute;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class QqeWindow : IDisposable
{
    private readonly int _length;
    private readonly MovingAvgType _kind;
    private readonly double _fast, _slow;
    private readonly RsiMean? _gain, _loss;
    private readonly Mean? _signal, _first, _second;
    private double _price, _rsi;
    private Number _previousSignal, _bull, _bear;
    private bool _hasPrevious;
    internal static long WidthPeriod(int length) => 2L * Math.Max(1, length) - 1;
    internal static void ValidateFactors(double fast, double slow)
    {
        if (double.IsNaN(fast) || double.IsInfinity(fast) || fast < 0) throw new ArgumentOutOfRangeException(nameof(fast));
        if (double.IsNaN(slow) || double.IsInfinity(slow) || slow < 0) throw new ArgumentOutOfRangeException(nameof(slow));
    }
    internal QqeWindow(MovingAvgType kind, int length, int smooth, double fast, double slow, bool external = false)
    {
        ValidateFactors(fast, slow); _kind = kind; _length = Math.Max(1, length); _fast = fast; _slow = slow;
        if (external) return;
        if (!StrengthWindow.Supports(kind)) throw new NotSupportedException("Native QQE requires SMA, WMA, EMA or Wilder smoothing.");
        _gain = new(kind, _length); _loss = new(kind, _length);
        _signal = new(kind, Math.Max(1, smooth)); _first = new(kind, WidthPeriod(length)); _second = new(kind, WidthPeriod(length));
    }
    internal (double Fast, double Slow, Signal Trade) Finish(Number width, bool final)
    {
        var fast = width * Number.Of(_fast); var slow = width * Number.Of(_slow); var order = (fast - slow).Sign;
        var bull = width - (order >= 0 ? fast : slow); var bear = width - (order >= 0 ? slow : fast);
        var trade = bull.Sign > 0 && (bull - _bull).Sign > 0 ? Signal.StrongBuy
            : bear.Sign < 0 && (bear - _bear).Sign < 0 ? Signal.StrongSell
            : bull.Sign > 0 ? Signal.Buy : bear.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _bull = bull; _bear = bear; }
        return (fast.Publish(), slow.Publish(), trade);
    }
    internal (double Fast, double Slow, Signal Trade) Next(double price, bool final)
    {
        StreamingInputValidation.Finite(price, nameof(price)); var change = _hasPrevious ? GainLossShare.Change(price, _price) : default;
        var gain = _gain!.Next(change.Mantissa > 0 ? change : default, final);
        var loss = _loss!.Next(change.Mantissa < 0 ? change.Absolute : default, final);
        var numerator = new ExactMeanAccumulator(); gain.AddTo(ref numerator, 100);
        var total = new ExactMeanAccumulator(); gain.AddTo(ref total); loss.AddTo(ref total);
#pragma warning disable S1244 // Only an exactly unchanged finite price preserves the prior RSI; nearby prices must update gains or losses.
        var preserve = _hasPrevious && _length > 1 && (_kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod) && price == _price;
#pragma warning restore S1244
        var rsi = preserve ? _rsi : GainLossShare.Of(numerator, total, 100);
        var signal = _signal!.Next(Number.Of(rsi), final); var movement = signal - _previousSignal;
        if (movement.Sign < 0) movement = movement.Times(-1);
        var result = Finish(_second!.Next(_first!.Next(movement, final), final), final);
        if (final) { _price = price; _rsi = rsi; _previousSignal = signal; _hasPrevious = true; }
        return result;
    }
    internal static (double[] Fast, double[] Slow, Signal[] Trades) Calculate(StockData data, MovingAvgType kind, int length, int smooth, double fast, double slow, bool callbacks)
    {
        ValidateFactors(fast, slow); var (prices, _, _, _, _) = CalculationsHelper.GetInputValuesList(data);
        foreach (var price in prices) StreamingInputValidation.Finite(price, nameof(prices));
        var external = callbacks && ComponentAverage.HasOverrides || !StrengthWindow.Supports(kind);
        using var window = new QqeWindow(kind, length, smooth, fast, slow, external);
        var first = new double[prices.Count]; var second = new double[prices.Count]; var trades = new Signal[prices.Count];
        if (!external)
        { for (var i = 0; i < prices.Count; i++) (first[i], second[i], trades[i]) = window.Next(prices[i], true); }
        else
        {
            // Existing callback and legacy APIs cannot express periods above Int32.MaxValue.
            var period = checked((int)WidthPeriod(length)); var caller = data.CaptureInputSeries();
            try
            {
                double[] rsi;
                if (callbacks)
                { using var context = new ComputeContext(); using var values = IndicatorCompute.ComputeRsiFast(data, context, length, kind); rsi = values.ToArray(); }
#pragma warning disable CS0618 // The legacy moving-average fallback deliberately preserves the existing batch RSI.
                else rsi = data.CalculateRelativeStrengthIndex(kind, Math.Max(1, length), Math.Max(1, smooth)).ChainedOutputs["Rsi"].ToArray();
#pragma warning restore CS0618
                data.RestoreInputSeries(caller);
                double[] Average(double[] values, int n)
                {
                    var result = (callbacks ? ComponentAverage.Take(values, n)?.ToArray() : null)
                        ?? CalculationsHelper.GetMovingAverageList(data, kind, n, values.ToList()).ToArray();
                    data.RestoreInputSeries(caller); return result;
                }
                var signal = Average(rsi, Math.Max(1, smooth));
                var changes = signal.Select((v, i) => { var d = Number.Of(v) - Number.Of(i == 0 ? 0 : signal[i - 1]); return (d.Sign < 0 ? d.Times(-1) : d).Publish(); }).ToArray();
                var widths = Average(Average(changes, period), period);
                for (var i = 0; i < prices.Count; i++) (first[i], second[i], trades[i]) = window.Finish(Number.Of(widths[i]), true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (first, second, trades);
    }
    private sealed class Mean
    {
        private readonly MovingAvgType _kind; private readonly long _period;
        private readonly Queue<Number> _history = new();
        private Number _sum, _weighted, _previous; private long _count;
        internal Mean(MovingAvgType kind, long period) { _kind = kind; _period = period; }
        internal Number Next(Number value, bool final)
        {
            var sum = _sum; var weighted = _weighted; Number result;
            var finite = _kind is MovingAvgType.SimpleMovingAverage or MovingAvgType.WeightedMovingAverage;
            if (finite)
            {
                weighted = weighted - sum + value.Times(_period);
                if (_history.Count == _period) sum -= _history.Peek(); sum += value;
                result = _kind == MovingAvgType.WeightedMovingAverage ? weighted.Divide(Number.Integer(new BigInteger(_period) * (_period + 1) / 2))
                    : _count + 1 < _period ? default : sum.Divide(_period);
            }
            else if (_kind == MovingAvgType.ExponentialMovingAverage && _count < _period) { sum += value; result = sum.Divide(_count + 1); }
            else result = (_previous.Times(_period - 1) + value.Times(_kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1)).Divide(_kind == MovingAvgType.ExponentialMovingAverage ? _period + 1 : _period);
            if (final)
            { if (finite) { if (_history.Count == _period) _history.Dequeue(); _history.Enqueue(value); } _sum = sum; _weighted = weighted; _previous = result; _count++; }
            return result;
        }
        internal void Reset() { _history.Clear(); _sum = _weighted = _previous = default; _count = 0; }
    }
    internal void Reset()
    { _gain?.Reset(); _loss?.Reset(); _signal?.Reset(); _first?.Reset(); _second?.Reset(); _price = _rsi = 0; _previousSignal = _bull = _bear = default; _hasPrevious = false; }
    public void Dispose() => Reset();
    private sealed class RsiMean
    {
        private readonly MovingAvgType _kind; private readonly int _length;
        private readonly Queue<StrengthValue> _history = new();
        private ExactMeanAccumulator _sum, _weighted; private StrengthValue _previous; private long _count;
        internal RsiMean(MovingAvgType kind, int length) { _kind = kind; _length = length; }
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
