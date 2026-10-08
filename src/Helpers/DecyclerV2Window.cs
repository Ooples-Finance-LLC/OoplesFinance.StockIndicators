namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DecyclerV2Window : IDisposable
{
    private readonly HighPassV2Window _fast, _slow;
    internal DecyclerV2Window(MovingAvgType kind, int fast, int slow)
    { _fast = new(kind, fast); _slow = new(kind, slow); }
    internal double Next(double price, bool commit)
    {
        var fast = _fast.Next(price, commit); var slow = _slow.Next(price, commit);
        var sum = new ExactMeanAccumulator(); slow.AddTo(ref sum); fast.AddTo(ref sum, -1);
        return sum.Mean(1);
    }
    internal void Reset() { _fast.Reset(); _slow.Reset(); }
    public void Dispose() { _fast.Dispose(); _slow.Dispose(); }
}
