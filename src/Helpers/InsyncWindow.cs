using System.Numerics;

namespace OoplesFinance.StockIndicators.Helpers;

// Components remain unpublished until their bounded votes are formed. Histories grow
// with observations, not with a potentially enormous configured period.
internal sealed class InsyncWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _cciLength, _mfiLength, _bbLength, _rocLength, _delay, _smaLength, _stochLength;
    private readonly double _mult;
    private readonly PriceRsiWindow _rsi;
    private readonly EaseWindow _ease;
    private readonly RocBankAverage _fast, _slow;
    private readonly Mean _bbMean, _dpoMean, _kMean, _dMean, _easeMean, _macdMean, _dpoVoteMean, _rocMean;
    private readonly Queue<double> _cci = new(), _bb = new(), _rocPrices = new(), _dpoPrices = new();
    private readonly Queue<(BigInteger Flow, int Direction)> _flows = new();
    private readonly Queue<(double Buy, double Sell)> _delayedVotes = new();
    private readonly LinkedList<(long Index, double Value)> _highs = new(), _lows = new();
    private BigInteger _cciSum, _bbSum, _bbSquares, _positive, _negative;
    private double _previousTypical;
    private long _count;

    internal InsyncWindow(int fastLength, int slowLength, int mfiLength, int bbLength,
        int cciLength, int dpoLength, int rocLength, int rsiLength, int stochLength,
        int stochKLength, int stochDLength, int smaLength, double stdDevMult, double divisor)
    {
        if (MathHelper.IsValueNullOrInfinity(stdDevMult)) throw new ArgumentOutOfRangeException(nameof(stdDevMult));
        _ease = new(divisor); _mult = stdDevMult;
        _cciLength = Math.Max(1, cciLength); _mfiLength = Math.Max(1, mfiLength);
        _bbLength = Math.Max(1, bbLength); _rocLength = Math.Max(1, rocLength);
        _smaLength = Math.Max(1, smaLength); _stochLength = Math.Max(1, stochLength);
        dpoLength = Math.Max(1, dpoLength);
        _delay = (int)Math.Max(2, Math.Min(530, (dpoLength + 1L) / 2 + 1));
        _rsi = new(MovingAvgType.WildersSmoothingMethod, Math.Max(1, rsiLength));
        _fast = new(MovingAvgType.ExponentialMovingAverage, fastLength, 1);
        _slow = new(MovingAvgType.ExponentialMovingAverage, slowLength, 1);
        _bbMean = new(_bbLength); _dpoMean = new(dpoLength);
        _kMean = new(stochKLength); _dMean = new(stochDLength);
        _easeMean = new(_smaLength, true); _macdMean = new(_smaLength, true);
        _dpoVoteMean = new(_smaLength, true); _rocMean = new(_smaLength, true);
    }

    internal static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal void Compute(StockData data, Span<double> output)
    {
        var custom = data.ChainedValues.Count > 0;
        var input = custom ? data.ChainedValues : data.InputValues;
        var ranges = custom ? CalculationsHelper.GetCustomRangeLists(input, data.HighPrices, data.LowPrices)
            : (data.HighPrices, data.LowPrices);
        for (var i = 0; i < input.Count; i++)
        {
            var typical = custom ? input[i] : CommodityIndexWindow.TypicalPrice(data.HighPrices[i], data.LowPrices[i], data.ClosePrices[i]);
            output[i] = Next(input[i], typical, data.HighPrices[i], data.LowPrices[i], data.Volumes[i], ranges.Item1[i], ranges.Item2[i], true);
        }
    }
    private static BigInteger U(double value) => ExactVarianceWindow.Units(value);
    private static RocBankValue Value(BigInteger units)
    {
        for (var shift = 0; ; shift += 1024)
        {
            var value = ExactMeanAccumulator.UnitRatio(units, BigInteger.One << shift);
            if (!double.IsInfinity(value)) return new(value, shift);
        }
    }
    private static RocBankValue Ratio(BigInteger numerator, BigInteger denominator) => denominator.IsZero ? default
        : Value(RocBankValue.RoundUnits(numerator * denominator.Sign * Unit, BigInteger.Abs(denominator)));
    private static RocBankValue Difference(RocBankValue left, RocBankValue right) => Value(RocBankValue.RoundUnits(Units(left) - Units(right), BigInteger.One));

    internal double Next(double price, double typical, double high, double low, double volume,
        double stochasticHigh, double stochasticLow, bool final)
    {
        var cciSum = _cciSum + U(typical) - (_cci.Count == _cciLength ? U(_cci.Peek()) : 0);
        var cci = default(RocBankValue);
        if (_cci.Count >= _cciLength - 1)
        {
            var residual = _cciLength * U(typical) - cciSum;
            var deviation = BigInteger.Abs(residual); var skip = _cci.Count == _cciLength;
            foreach (var old in _cci)
            {
                if (skip) { skip = false; continue; }
                deviation += BigInteger.Abs(_cciLength * U(old) - cciSum);
            }
            cci = Ratio(_cciLength * residual * Unit, U(.015) * deviation);
        }

        var direction = _count == 0 ? 0 : typical.CompareTo(_previousTypical);
        var flow = U(typical) * U(volume);
        var positive = _positive + (direction > 0 ? flow : 0);
        var negative = _negative + (direction < 0 ? flow : 0);
        if (_flows.Count == _mfiLength)
        {
            var expired = _flows.Peek();
            if (expired.Direction > 0) positive -= expired.Flow;
            if (expired.Direction < 0) negative -= expired.Flow;
        }
        var mfi = negative.IsZero ? 100 : positive.IsZero ? 0 : Math.Max(0, Math.Min(100, Ratio(100 * positive, positive + negative).Publish()));

        var middle = _bbMean.Next(new(price), final);
        var expiredPrice = _bb.Count == _bbLength ? U(_bb.Peek()) : BigInteger.Zero;
        var bbSum = _bbSum + U(price) - expiredPrice;
        var bbSquares = _bbSquares + U(price) * U(price) - expiredPrice * expiredPrice;
        var n = new BigInteger(_bbLength);
        var deviationValue = _bb.Count < _bbLength - 1 ? 0 : ExactPopulationDeviation.RootRatio(n * bbSquares - bbSum * bbSum, n * n);
        var width = U(deviationValue) * U(_mult);
        var percent = width.IsZero ? default : Ratio(100 * ((U(price) - Units(middle)) * Unit + width), 2 * width);

        var macd = Difference(_fast.Next(new(price), final), _slow.Next(new(price), final));
        var dpo = Difference(new(_dpoPrices.Count == _delay ? _dpoPrices.Peek() : 0), _dpoMean.Next(new(price), final));
        var roc = _rocPrices.Count < _rocLength ? default : RocBankValue.Return(price, _rocPrices.Peek());
        var ease = _ease.Next(high, low, volume, final);
        var easeMean = _easeMean.Next(ease, final); var macdMean = _macdMean.Next(macd, final);
        var dpoMean = _dpoVoteMean.Next(dpo, final); var rocMean = _rocMean.Next(roc, final);

        var highest = Extreme(_highs, stochasticHigh, true, final);
        var lowest = Extreme(_lows, stochasticLow, false, final);
        var stochastic = Math.Max(0, Math.Min(100, Ratio(100 * (U(price) - U(lowest)), U(highest) - U(lowest)).Publish()));
        var k = _kMean.Next(new(stochastic), final); var d = _dMean.Next(k, final);
        var rsi = _rsi.Next(price, final);
        var buy = InsyncVotes.Direction(dpo, dpoMean); var sell = InsyncVotes.InverseDirection(dpo, dpoMean);
        var delayed = _delayedVotes.Count == _smaLength ? _delayedVotes.Peek() : default;
        var result = 50 + InsyncVotes.Band(cci, -100, 100) + InsyncVotes.Band(percent, 5, 95)
            + InsyncVotes.Band(rsi, 30, 70) + InsyncVotes.Band(mfi, 20, 80)
            + InsyncVotes.Band(k, 20, 80) + InsyncVotes.Band(d, 20, 80)
            + InsyncVotes.Direction(ease, easeMean) + InsyncVotes.Direction(macd, macdMean)
            + InsyncVotes.Direction(roc, rocMean) + delayed.Buy + delayed.Sell;
        if (final)
        {
            Push(_cci, typical, _cciLength); _cciSum = cciSum;
            Push(_bb, price, _bbLength); _bbSum = bbSum; _bbSquares = bbSquares;
            Push(_flows, (flow, direction), _mfiLength); _positive = positive; _negative = negative;
            Push(_rocPrices, price, _rocLength); Push(_dpoPrices, price, _delay);
            Push(_delayedVotes, (buy, sell), _smaLength);
            _previousTypical = typical; _count++;
        }
        return result;
    }

    private static void Push<T>(Queue<T> queue, T value, int length)
    { if (queue.Count == length) queue.Dequeue(); queue.Enqueue(value); }
    private double Extreme(LinkedList<(long Index, double Value)> deque, double value, bool maximum, bool final)
    {
        var expiry = _count - _stochLength + 1L; var first = deque.First;
        while (first is not null && first.Value.Index < expiry) first = first.Next;
        var result = first is null ? value : maximum ? Math.Max(value, first.Value.Value) : Math.Min(value, first.Value.Value);
        if (final)
        {
            while (deque.First is { } old && old.Value.Index < expiry) deque.RemoveFirst();
            while (deque.Last is { } last && (maximum ? last.Value.Value <= value : last.Value.Value >= value)) deque.RemoveLast();
            deque.AddLast((_count, value));
        }
        return result;
    }
    internal void Reset()
    {
        _rsi.Reset(); _ease.Reset(); _fast.Reset(); _slow.Reset();
        foreach (var mean in new[] { _bbMean, _dpoMean, _kMean, _dMean, _easeMean, _macdMean, _dpoVoteMean, _rocMean }) mean.Reset();
        _cci.Clear(); _bb.Clear(); _rocPrices.Clear(); _dpoPrices.Clear(); _flows.Clear(); _delayedVotes.Clear(); _highs.Clear(); _lows.Clear();
        _cciSum = _bbSum = _bbSquares = _positive = _negative = 0; _previousTypical = 0; _count = 0;
    }
    public void Dispose() { _rsi.Dispose(); _fast.Dispose(); _slow.Dispose(); }

    private sealed class Mean
    {
        private readonly int _length; private readonly bool _partial;
        private readonly Queue<RocBankValue> _history = new(); private ExactMeanAccumulator _sum;
        internal Mean(int length, bool partial = false) { _length = Math.Max(1, length); _partial = partial; }
        internal RocBankValue Next(RocBankValue value, bool final)
        {
            var sum = _sum;
            if (_history.Count == _length) _history.Peek().AddTo(ref sum, -1);
            value.AddTo(ref sum);
            var count = Math.Min(_history.Count + 1L, _length);
            var result = !_partial && count < _length ? default : RocBankValue.Round(sum, count: count);
            if (final) { _sum = sum; Push(_history, value, _length); }
            return result;
        }
        internal void Reset() { _sum = default; _history.Clear(); }
    }
}
