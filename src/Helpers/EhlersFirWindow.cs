namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EhlersFirWindow : IDisposable
{
    private readonly double[] _coefficients;
    private readonly ExactMeanAccumulator _denominator;
    private readonly PooledRingBuffer<double> _history;
    internal EhlersFirWindow(double coef1 = 1, double coef2 = 3.5, double coef3 = 4.5,
        double coef4 = 3, double coef5 = .5, double coef6 = -.5, double coef7 = -1.5)
    {
        _coefficients = new[] { coef1, coef2, coef3, coef4, coef5, coef6, coef7 };
        var sum = new ExactMeanAccumulator();
        for (var i = 0; i < _coefficients.Length; i++)
        {
            var coefficient = _coefficients[i];
            if (double.IsNaN(coefficient) || double.IsInfinity(coefficient)) throw new ArgumentOutOfRangeException($"coef{i + 1}", "FIR coefficients must be finite.");
            sum.Add(coefficient);
        }
        if (sum.IsExactlyZero) throw new ArgumentException("FIR coefficients must have a nonzero sum.");
        _denominator = sum; _history = new(6);
    }
    internal double Next(double price, bool commit)
    {
        var numerator = new ExactMeanAccumulator(); numerator.AddProduct(price, _coefficients[0]);
        for (var lag = 1; lag <= _history.Count; lag++) numerator.AddProduct(_history[_history.Count - lag], _coefficients[lag]);
        var result = numerator.Ratio(_denominator);
        if (commit) _history.TryAdd(price, out _);
        return result;
    }
    internal void Reset() => _history.Clear();
    public void Dispose() => _history.Dispose();
}
