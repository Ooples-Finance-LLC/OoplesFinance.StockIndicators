using System.Numerics;
using OoplesFinance.StockIndicators.Enums;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class HilbertCycleWindow
{
    private readonly HilbertTransformerWindow _normalizer;
    private readonly int _upper, _minimum, _horizon, _mode;
    private readonly BigInteger _gain, _feedback1, _feedback2;
    private readonly List<double> _advances = new();
    private double _real, _imaginary, _phase, _period;
    private Scaled _output1, _output2;
    internal HilbertCycleWindow(int upper, int lower, int minimum, int horizon, int mode)
    {
        _upper = Math.Max(1, upper); lower = Math.Max(1, lower); _minimum = Math.Max(1, minimum); _horizon = Math.Min(360, Math.Max(1, horizon)); _mode = mode; _normalizer = new(_upper, lower, 1, false);
        var angle = 1.414 * Math.PI / lower; var radius = ExactVarianceWindow.Units(Math.Exp(-angle)); var cosine = ExactVarianceWindow.Units(Math.Cos(angle)); _feedback1 = 2 * radius * cosine; _feedback2 = -radius * radius; _gain = (BigInteger.One << 2148) - _feedback1 - _feedback2;
    }
    private readonly struct Scaled
    {
        internal readonly double Mantissa; internal readonly int Shift;
        internal Scaled(double mantissa, int shift) { Mantissa = mantissa; Shift = shift; }
        internal void Add(ref ExactMeanAccumulator sum, double coefficient = 1)
        { var negative = new ExactMeanAccumulator(); negative.AddProduct(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal void Add(ref ExactMeanAccumulator sum, BigInteger coefficient)
        { var negative = new ExactMeanAccumulator(); negative.Add(Mantissa, -coefficient); negative.ScaleByPowerOfTwo(Shift); sum.Subtract(negative); }
        internal static Scaled Round(ExactMeanAccumulator sum, long divisor = 1)
        {
            if (sum.IsExactlyZero) return default;
            var shift = 0; var value = sum.Mean(divisor); var lower = Math.Pow(2, -256); var upper = Math.Pow(2, 256);
            while (Math.Abs(value) < lower) { sum.ScaleByPowerOfTwo(512); shift -= 512; value = sum.Mean(divisor); }
            while (double.IsInfinity(value) || Math.Abs(value) >= upper) { sum.ScaleByPowerOfTwo(-512); shift += 512; value = sum.Mean(divisor); }
            return new(value, shift);
        }
    }
    private static double Publish(Scaled value) { var sum = new ExactMeanAccumulator(); value.Add(ref sum); return sum.Mean(1); }
    private double Bound(double value) => Math.Min(_upper, Math.Max(_minimum, value));
    internal (double Value, Signal Signal) Next(double price, bool commit)
    {
        var vector = _normalizer.Next(price, commit); var phase = _phase; var advance = 0d; double period;
        if (_mode == 2)
        {
            phase = Math.Atan2(vector.Imaginary, vector.Real) * (180 / Math.PI); if (phase < 0) phase += 360;
            advance = Bound(_phase < 90 && phase > 270 ? 360 + _phase - phase : _phase - phase);
            var total = advance; period = total >= 360 - 3.6e-7 ? 1 : 0;
            for (var lag = 1; period == 0 && lag < _horizon && lag <= _advances.Count; lag++) { total += _advances[_advances.Count - lag]; if (total >= 360 - 3.6e-7) period = lag + 1; }
            if (period == 0) period = _period;
        }
        else
        {
            var determinant = new ExactMeanAccumulator(); determinant.AddProduct(vector.Real, _imaginary); determinant.AddProduct(-vector.Imaginary, _real);
            if (_mode == 1)
            {
                var dot = new ExactMeanAccumulator(); dot.AddProduct(vector.Real, _real); dot.AddProduct(vector.Imaginary, _imaginary); var angle = Math.Abs(Math.Atan2(-determinant.Mean(1), dot.Mean(1))); period = Bound(angle == 0 ? 0 : 2 * Math.PI / angle);
            }
            else
            {
                var magnitude = new ExactMeanAccumulator(); magnitude.AddProduct(Math.Abs(vector.Real), Math.Abs(_imaginary)); magnitude.AddProduct(Math.Abs(vector.Imaginary), Math.Abs(_real));
                var resolved = !magnitude.IsExactlyZero && Math.Abs(determinant.Ratio(magnitude)) > 1e-12;
                var energy = new ExactMeanAccumulator(); energy.AddProduct(vector.Real, vector.Real); energy.AddProduct(vector.Imaginary, vector.Imaginary); period = Bound(resolved ? (2 * Math.PI) * energy.Ratio(determinant) : 0);
            }
        }
        var sum = new ExactMeanAccumulator(); sum.Add(period, _gain); sum.Add(_period, _gain); _output1.Add(ref sum, 2 * _feedback1); _output2.Add(ref sum, 2 * _feedback2); sum.ScaleByPowerOfTwo(-2149); var output = Scaled.Round(sum);
        if (commit) { _real = vector.Real; _imaginary = vector.Imaginary; _phase = phase; _period = period; _output2 = _output1; _output1 = output; if (_mode == 2) { if (_advances.Count == _horizon) _advances.RemoveAt(0); _advances.Add(advance); } }
        return (Publish(output), vector.Signal);
    }
    internal void Reset() { _normalizer.Reset(); _advances.Clear(); _real = _imaginary = _phase = _period = 0; _output1 = _output2 = default; }
}
