namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class HullEstimateWindow : IDisposable
{
    private readonly RocBankAverage _weighted, _exponential;
    internal HullEstimateWindow(int length)
    {
        var period = Math.Max(2, Math.Min(530, (int)Math.Ceiling(length / 2d)));
        _weighted = new(MovingAvgType.WeightedMovingAverage, period, period);
        _exponential = new(MovingAvgType.ExponentialMovingAverage, period, period);
    }
    internal double Next(double price, bool commit)
    {
        var input = new RocBankValue(price); var weighted = _weighted.Next(input, commit); var exponential = _exponential.Next(input, commit);
        var sum = new ExactMeanAccumulator(); weighted.AddTo(ref sum, 3); exponential.AddTo(ref sum, -2);
        return sum.Mean(1);
    }
    internal static double Combine(double weighted, double exponential)
    {
        var sum = new ExactMeanAccumulator(); sum.Add(weighted, 3); sum.Add(exponential, -2); return sum.Mean(1);
    }
    internal void Reset() { _weighted.Reset(); _exponential.Reset(); }
    public void Dispose() { _weighted.Dispose(); _exponential.Dispose(); }
}
