namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class LaguerreRsiWindow
{
    private readonly double _gamma, _gain;
    private double _baseline;
    private bool _hasBaseline;
    private Scaled _l0, _l1, _l2, _l3;
    internal LaguerreRsiWindow(double gamma)
    {
        if (double.IsNaN(gamma) || double.IsInfinity(gamma)) throw new ArgumentOutOfRangeException(nameof(gamma));
        _gamma = Math.Max(0, Math.Min(1, gamma)); _gain = 1 - _gamma;
    }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(1); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(1); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(1); }
            return new(value, shift);
        }
    }
    private Scaled Stage(Scaled current, Scaled previous, Scaled history)
    { var sum = new ExactMeanAccumulator(); current.Add(ref sum, -_gamma); previous.Add(ref sum); history.Add(ref sum, _gamma); return Scaled.Round(sum); }
    private static void Direction(Scaled left, Scaled right, ref ExactMeanAccumulator up, ref ExactMeanAccumulator down)
    {
        var difference = new ExactMeanAccumulator(); left.Add(ref difference); right.Add(ref difference, -1);
        if (difference.Sign < 0) down.Subtract(difference);
        else { var negative = new ExactMeanAccumulator(); negative.Subtract(difference); up.Subtract(negative); }
    }
    internal double Next(double price, bool commit)
    {
        var centered = new ExactMeanAccumulator(); if (_hasBaseline) { centered.Add(price); centered.Add(_baseline, -1); }
        var first = new ExactMeanAccumulator(); Scaled.Round(centered).Add(ref first, _gain); _l0.Add(ref first, _gamma); var l0 = Scaled.Round(first);
        var l1 = Stage(l0, _l0, _l1); var l2 = Stage(l1, _l1, _l2); var l3 = Stage(l2, _l2, _l3);
        var up = new ExactMeanAccumulator(); var down = new ExactMeanAccumulator();
        Direction(l0, l1, ref up, ref down); Direction(l1, l2, ref up, ref down); Direction(l2, l3, ref up, ref down);
        var negativeDown = new ExactMeanAccumulator(); negativeDown.Subtract(down); var total = up; total.Subtract(negativeDown);
        var result = total.IsExactlyZero ? 0 : up.Ratio(total);
        if (commit) { if (!_hasBaseline) _baseline = price; _hasBaseline = true; _l0 = l0; _l1 = l1; _l2 = l2; _l3 = l3; }
        return result;
    }
    internal void Reset() { _baseline = 0; _hasBaseline = false; _l0 = _l1 = _l2 = _l3 = default; }
}
