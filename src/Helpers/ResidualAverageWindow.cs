namespace OoplesFinance.StockIndicators.Helpers;

// Rounded convex stages carry one extra exponent bit until each output is published.
internal sealed class ResidualAverageWindow : IDisposable
{
    private readonly StrengthAverage _mean;
    private readonly StrengthAverage[] _stages;
    internal ResidualAverageWindow(MovingAvgType meanKind, int meanLength, MovingAvgType smoothingKind, params int[] lengths)
    {
        _mean = new StrengthAverage(meanKind, meanLength);
        _stages = lengths.Select(length => new StrengthAverage(smoothingKind, length)).ToArray();
    }
    internal (double Value, double Signal) Next(double value, bool commit)
    {
        var mean = _mean.Next(new StrengthValue(value), commit);
        var residual = new ExactMeanAccumulator(); residual.Add(value); mean.AddTo(ref residual, -1);
        var current = StrengthValue.Round(residual, 1); var primary = current;
        for (var i = 0; i < _stages.Length; i++)
        {
            current = _stages[i].Next(current, commit);
            if (i == Math.Max(0, _stages.Length - 2)) primary = current;
        }
        return (Publish(primary), Publish(current));
    }
    private static double Publish(StrengthValue value) => value.Mantissa * (value.Doubled ? 2 : 1);
    internal void Reset() { _mean.Reset(); foreach (var stage in _stages) stage.Reset(); }
    public void Dispose() { _mean.Dispose(); foreach (var stage in _stages) stage.Dispose(); }
}
