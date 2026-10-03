using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ClassicHilbertWindow
{
    private static readonly double[] Weights = { .091, .111, .143, .2, .333, 1, -1, -.333, -.2, -.143, -.111, -.091 };
    private readonly HilbertTransformerWindow _normalizer;
    private readonly List<double> _history = new();
    internal ClassicHilbertWindow(int upper, int lower) => _normalizer = new(upper, lower, 1, false);
    internal (double Real, double Imaginary, Signal Signal) Next(double value, bool commit)
    {
        var point = _normalizer.Next(value, commit); var sum = new ExactMeanAccumulator();
        for (var tap = 0; tap < Weights.Length; tap++) { var lag = 2 * tap; var sample = lag == 0 ? point.Real : lag <= _history.Count ? _history[_history.Count - lag] : 0; sum.AddProduct(sample, Weights[tap]); }
        var divisor = new ExactMeanAccumulator(); divisor.Add(1.865); var imaginary = sum.Ratio(divisor);
        if (commit) { if (_history.Count == 22) _history.RemoveAt(0); _history.Add(point.Real); }
        return (point.Real, imaginary, point.Signal);
    }
    internal void Reset() { _normalizer.Reset(); _history.Clear(); }
}
