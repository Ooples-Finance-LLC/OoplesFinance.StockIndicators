namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class DirectionalStrengthWindow : IDisposable
{
    private readonly StrengthWindow _strength;
    private double _high, _low;
    internal DirectionalStrengthWindow(MovingAvgType kind, int length1, int length2 = 10, int length3 = 5, int capacityHint = int.MaxValue)
        => _strength = new StrengthWindow(kind, new[] { length1, length2, length3 }, capacityHint);

    internal double Next(double high, double low, bool final)
    {
        var up = new ExactMeanAccumulator();
        if (high > _high) { up.Add(high); up.Add(_high, -1); }
        var down = new ExactMeanAccumulator();
        if (low < _low) { down.Add(_low); down.Add(low, -1); }
        var net = new ExactMeanAccumulator();
        StrengthValue.Round(up, 1).AddTo(ref net);
        StrengthValue.Round(down, 1).AddTo(ref net, -1);
        var result = _strength.NextChange(StrengthValue.Round(net, 1), final);
        if (final) { _high = high; _low = low; }
        return result;
    }

    internal void Reset() { _strength.Reset(); _high = _low = 0; }
    public void Dispose() => _strength.Dispose();
}
