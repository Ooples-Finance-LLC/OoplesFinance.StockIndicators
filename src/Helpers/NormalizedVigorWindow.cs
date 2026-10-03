using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class NormalizedVigorWindow : IDisposable
{
    private readonly Average _body, _range, _signal;
    private readonly PooledRingBuffer<BigInteger> _bodies, _ranges;
    private readonly int _length;
    private BigInteger _bodySum, _rangeSum;
    internal NormalizedVigorWindow(MovingAvgType kind, int length, int capacityHint = int.MaxValue)
    {
        _length = Math.Max(1, length);
        _body = new(kind, _length, capacityHint); _range = new(kind, _length, capacityHint); _signal = new(kind, _length, capacityHint);
        _bodies = new(Math.Min(_length, Math.Max(1, capacityHint))); _ranges = new(Math.Min(_length, Math.Max(1, capacityHint)));
    }
    private static BigInteger Units(RocBankValue value) => ExactVarianceWindow.Units(value.Mantissa) << value.UpperShift;
    internal static RocBankValue Difference(double left, double right)
    { var sum = new ExactMeanAccumulator(); sum.Add(left); sum.Add(right, -1); return RocBankValue.Round(sum); }
    private static RocBankValue Value(BigInteger units, BigInteger divisor)
    {
        var rounded = RocBankValue.RoundUnits(units, divisor);
        for(var shift=0;;shift+=1024) { var v=ExactMeanAccumulator.UnitRatio(rounded,BigInteger.One<<shift); if(!double.IsInfinity(v)) return new(v,shift); }
    }
    internal RocBankValue Ratio(RocBankValue body, RocBankValue range, bool commit)
    {
        var b = Units(body); var r = Units(range);
        var bodySum = _bodySum + b - (_bodies.Count >= _length ? _bodies[0] : BigInteger.Zero);
        var rangeSum = _rangeSum + r - (_ranges.Count >= _length ? _ranges[0] : BigInteger.Zero);
        var ratio = rangeSum.IsZero ? default : Value((100 * bodySum * rangeSum.Sign) << 1074, BigInteger.Abs(rangeSum));
        if(commit) { _bodySum=bodySum; _rangeSum=rangeSum; _bodies.TryAdd(b,out _); _ranges.TryAdd(r,out _); }
        return ratio;
    }
    internal (double Value, double Signal) Next(double close, double open, double high, double low, bool commit)
    {
        var body=_body.Next(Difference(close,open),commit); var range=_range.Next(Difference(high,low),commit);
        var value=Ratio(body,range,commit); var signal=_signal.Next(value,commit); return(value.Publish(),signal.Publish());
    }
    internal void Reset() { _body.Reset(); _range.Reset(); _signal.Reset(); _bodySum=_rangeSum=BigInteger.Zero; _bodies.Clear(); _ranges.Clear(); }
    public void Dispose() { _body.Dispose(); _range.Dispose(); _signal.Dispose(); _bodies.Dispose(); _ranges.Dispose(); }

    // A symmetric triangle is the convolution of two box windows. Keep both sums
    // exact and round only after dividing by the complete triangular weight.
    private sealed class Average : IDisposable
    {
        private readonly RocBankAverage? _basic;
        private readonly IMovingAverageSmoother? _fallback;
        private readonly PooledRingBuffer<BigInteger>? _a, _b;
        private readonly int _aLength, _bLength;
        private BigInteger _aSum, _bSum;
        internal Average(MovingAvgType kind,int length,int capacityHint)
        {
            if(StrengthWindow.Supports(kind)) _basic=new(kind,length,capacityHint);
            else if(kind==MovingAvgType.SymmetricallyWeightedMovingAverage)
            {
                _aLength=(int)((length+1L)/2); _bLength=length-_aLength+1;
                _a=new(Math.Min(_aLength,Math.Max(1,capacityHint))); _b=new(Math.Min(_bLength,Math.Max(1,capacityHint)));
            }
            else _fallback=MovingAverageSmootherFactory.Create(kind,length);
        }
        internal RocBankValue Next(RocBankValue value,bool commit)
        {
            if(_basic is not null) return _basic.Next(value,commit);
            if(_a is null) return new(_fallback!.Next(value.Publish(),commit));
            var input=Units(value); var aSum=_aSum+input-(_a.Count>=_aLength?_a[0]:BigInteger.Zero);
            var bSum=_bSum+aSum-(_b!.Count>=_bLength?_b[0]:BigInteger.Zero);
            var result=Value(bSum,(long)_aLength*_bLength);
            if(commit) { _aSum=aSum; _bSum=bSum; _a.TryAdd(input,out _); _b.TryAdd(aSum,out _); }
            return result;
        }
        internal void Reset() { _basic?.Reset(); _fallback?.Reset(); _a?.Clear(); _b?.Clear(); _aSum=_bSum=BigInteger.Zero; }
        public void Dispose() { _basic?.Dispose(); _fallback?.Dispose(); _a?.Dispose(); _b?.Dispose(); }
    }
}
