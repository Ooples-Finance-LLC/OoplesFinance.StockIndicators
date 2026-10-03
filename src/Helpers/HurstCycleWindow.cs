namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HurstCycleWindow : IDisposable
{
    private readonly KeltnerWindow? _fast, _slow;
    private readonly PooledRingBuffer<RocBankValue> _fastHistory, _slowHistory;
    private readonly double _fastFactor, _slowFactor;
    internal static int HalfCycle(int length) => (int)Math.Min(530, Math.Max(2, (Math.Max(1, length) + 1L) / 2));
    internal HurstCycleWindow(MovingAvgType kind, int fastLength, int slowLength, double fastFactor, double slowFactor, bool external = false, int capacityHint = int.MaxValue)
    {
        HighLowBandsWindow.ValidateShift(fastFactor); HighLowBandsWindow.ValidateShift(slowFactor); _fastFactor = fastFactor; _slowFactor = slowFactor;
        var fastCycle = HalfCycle(fastLength); var slowCycle = HalfCycle(slowLength); _fastHistory = new(HalfCycle(fastCycle)); _slowHistory = new(HalfCycle(slowCycle));
        if (!external) { _fast = new(kind, fastCycle, fastCycle, kind, capacityHint); _slow = new(kind, slowCycle, slowCycle, kind, capacityHint); }
    }
    private static RocBankValue Boundary(RocBankValue center, RocBankValue atr, double factor, int sign)
    { var sum = new ExactMeanAccumulator(); sum.AddProduct(atr.Mantissa, factor, sign); sum.ScaleByPowerOfTwo(atr.UpperShift); center.AddTo(ref sum); return RocBankValue.Round(sum); }
    private static RocBankValue Midpoint(RocBankValue upper, RocBankValue lower)
    { var sum = new ExactMeanAccumulator(); upper.AddTo(ref sum); lower.AddTo(ref sum); return RocBankValue.Round(sum, count: 2); }
    private static double Position(RocBankValue value, RocBankValue upper, RocBankValue lower)
    { var numerator = new ExactMeanAccumulator(); value.AddTo(ref numerator); lower.AddTo(ref numerator, -1); var denominator = new ExactMeanAccumulator(); upper.AddTo(ref denominator); lower.AddTo(ref denominator, -1); return denominator.IsExactlyZero ? 0 : numerator.Ratio(denominator); }
    internal (double FastUpper, double FastMiddle, double FastLower, double SlowUpper, double SlowMiddle, double SlowLower, double OMed, double OShort) Next(double high, double low, double close, bool commit, RocBankValue? fastAtr = null, RocBankValue? slowAtr = null, RocBankValue? fastMean = null, RocBankValue? slowMean = null)
    {
        var fast = _fast?.Next(high, low, close, commit) ?? default; var slow = _slow?.Next(high, low, close, commit) ?? default;
        var fastCenter = _fastHistory.Count == _fastHistory.Capacity ? _fastHistory[0] : new RocBankValue(close); var slowCenter = _slowHistory.Count == _slowHistory.Capacity ? _slowHistory[0] : new RocBankValue(close);
        var fu = Boundary(fastCenter, fastAtr ?? fast.Atr, _fastFactor, 1); var fl = Boundary(fastCenter, fastAtr ?? fast.Atr, _fastFactor, -1); var su = Boundary(slowCenter, slowAtr ?? slow.Atr, _slowFactor, 1); var sl = Boundary(slowCenter, slowAtr ?? slow.Atr, _slowFactor, -1);
        var fm = Midpoint(fu, fl); var sm = Midpoint(su, sl);
        if (commit) { _fastHistory.TryAdd(fastMean ?? fast.Middle, out _); _slowHistory.TryAdd(slowMean ?? slow.Middle, out _); }
        return (fu.Publish(), fm.Publish(), fl.Publish(), su.Publish(), sm.Publish(), sl.Publish(), Position(fm, su, sl), Position(new RocBankValue(close), su, sl));
    }
    internal void Reset() { _fast?.Reset(); _slow?.Reset(); _fastHistory.Clear(); _slowHistory.Clear(); }
    public void Dispose() { _fast?.Dispose(); _slow?.Dispose(); _fastHistory.Dispose(); _slowHistory.Dispose(); }
}
