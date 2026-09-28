namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class EvenBetterSineWaveKernel
{
    private readonly double _pole, _gain, _c1, _c2, _c3;
    private double _price;
    private Scaled _change, _high, _filter1, _filter2;
    private bool _hasPrice;
    internal EvenBetterSineWaveKernel(int highPeriod, int lowPeriod)
    {
        var angle = Math.Max(.01, Math.Min(.99, 2 * Math.PI / Math.Max(1, highPeriod)));
        _pole = Math.Cos(angle) / (1 + Math.Sin(angle));
        _gain = (1 + _pole) / 2;
        var lowAngle = Math.Max(.01, Math.Min(.99, 1.414 * Math.PI / Math.Max(1, lowPeriod)));
        var radius = Math.Exp(-lowAngle);
        _c2 = 2 * radius * Math.Cos(lowAngle); _c3 = -radius * radius; _c1 = 1 - _c2 - _c3;
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
    private static void Square(ref ExactMeanAccumulator sum, Scaled value, int shift)
    { var negative = new ExactMeanAccumulator(); negative.AddProduct(value.Mantissa, value.Mantissa, -3); negative.ScaleByPowerOfTwo(2 * (value.Shift - shift)); sum.Subtract(negative); }
    internal double Next(double value, bool final)
    {
        var delta = new ExactMeanAccumulator(); if (_hasPrice) { delta.Add(value); delta.Add(_price, -1); } var change = Scaled.Round(delta);
        // Preserve the DC/Nyquist zeros ahead of the recursive pole.
        var drive = new ExactMeanAccumulator(); change.Add(ref drive); _change.Add(ref drive); drive.ScaleByPowerOfTwo(-1);
        var highSum = new ExactMeanAccumulator(); Scaled.Round(drive).Add(ref highSum, _gain); _high.Add(ref highSum, _pole); var high = Scaled.Round(highSum);
        var filterSum = new ExactMeanAccumulator(); high.Add(ref filterSum, _c1); _filter1.Add(ref filterSum, _c2); _filter2.Add(ref filterSum, _c3); var filtered = Scaled.Round(filterSum);
        var shift = Math.Max(filtered.Mantissa == 0 ? int.MinValue : filtered.Shift,
            Math.Max(_filter1.Mantissa == 0 ? int.MinValue : _filter1.Shift, _filter2.Mantissa == 0 ? int.MinValue : _filter2.Shift));
        var result = 0d;
        if (shift != int.MinValue)
        {
            var sum = new ExactMeanAccumulator(); filtered.Add(ref sum); _filter1.Add(ref sum); _filter2.Add(ref sum); sum.ScaleByPowerOfTwo(-shift);
            var power = new ExactMeanAccumulator(); Square(ref power, filtered, shift); Square(ref power, _filter1, shift); Square(ref power, _filter2, shift);
            var denominator = new ExactMeanAccumulator(); denominator.Add(power.SqrtMean(1)); result = sum.Ratio(denominator);
        }
        if (final) { _price = value; _change = change; _high = high; _filter2 = _filter1; _filter1 = filtered; _hasPrice = true; }
        return result;
    }
    internal void Reset() { _price = 0; _change = _high = _filter1 = _filter2 = default; _hasPrice = false; }
}
