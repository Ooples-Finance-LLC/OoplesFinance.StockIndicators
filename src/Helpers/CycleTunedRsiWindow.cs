namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class CycleTunedRsiWindow
{
    private readonly AdaptiveCyberWindow _period;
    private Scaled _gain, _loss;
    private double _previous;
    private bool _hasPrevious;
    internal CycleTunedRsiWindow(int length) => _period = new(length, .07);
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
    private static Scaled Average(Scaled change, Scaled previous, double weight)
    {
        var sum = new ExactMeanAccumulator(); previous.Add(ref sum); change.Add(ref sum, weight); previous.Add(ref sum, -weight); return Scaled.Round(sum);
    }
    internal double Next(double price, bool commit)
    {
        var period = _period.Next(price, commit).Period; var weight = period == 0 ? .07 : 1 / period;
        var difference = new ExactMeanAccumulator(); difference.Add(price); difference.Add(_previous, -1); var change = Scaled.Round(difference);
        var gainChange = change.Mantissa > 0 ? change : default; var lossChange = change.Mantissa < 0 ? new Scaled(-change.Mantissa, change.Shift) : default;
        var gain = Average(gainChange, _hasPrevious ? _gain : gainChange, weight); var loss = Average(lossChange, _hasPrevious ? _loss : lossChange, weight);
        var numerator = new ExactMeanAccumulator(); gain.Add(ref numerator, 100); var denominator = new ExactMeanAccumulator(); gain.Add(ref denominator); loss.Add(ref denominator);
        var result = loss.Mantissa == 0 ? 100 : gain.Mantissa == 0 ? 0 : numerator.Ratio(denominator);
        if (commit) { _gain = gain; _loss = loss; _previous = price; _hasPrevious = true; }
        return result;
    }
    internal void Reset() { _period.Reset(); _gain = _loss = default; _previous = 0; _hasPrevious = false; }
}
