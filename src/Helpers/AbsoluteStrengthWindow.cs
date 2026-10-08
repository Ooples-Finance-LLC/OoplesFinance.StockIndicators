using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AbsoluteStrengthWindow
{
    private readonly int _length, _meanLength, _signalLength;
    private BigInteger _gains, _ties, _losses;
    private double _price, _mean, _first, _second;
    private bool _started;
    internal AbsoluteStrengthWindow(int length, int meanLength, int signalLength)
    { _length = Math.Max(1, length); _meanLength = Math.Max(1, meanLength); _signalLength = Math.Max(1, signalLength); }
    private static BigInteger Units(double value) => ExactVarianceWindow.Units(value);
    private static double Blend(double value, double previous, int length) =>
        ExactMeanAccumulator.UnitRatio(2 * Units(value) + (length - 1L) * Units(previous), new BigInteger(length + 1L));
    internal double Next(double price, bool commit)
    {
        if (!(price > 0) || double.IsInfinity(price)) throw new ArgumentOutOfRangeException(nameof(price), "Absolute Strength Index requires strictly positive finite effective prices.");
        var current = Units(price); var previous = Units(_price);
        var gains = _gains; var losses = _losses; var ties = _ties;
        if (_started)
        {
            if (price > _price) gains = RocBankValue.RoundUnits(gains + RocBankValue.RoundUnits((current - previous) << 1074, previous), BigInteger.One);
            else if (price < _price) losses = RocBankValue.RoundUnits(losses + RocBankValue.RoundUnits((previous - current) << 1074, current), BigInteger.One);
            else ties = RocBankValue.RoundUnits(ties + Units(1d / _length), BigInteger.One);
        }
        var strength = (losses + ties).IsZero ? 1 : ExactMeanAccumulator.UnitRatio((gains + ties) << 1074, gains + losses + 2 * ties);
        var mean = Blend(strength, _mean, _meanLength);
        var residual = ExactMeanAccumulator.UnitRatio(Units(strength) - Units(mean), BigInteger.One);
        var first = Blend(residual, _first, _signalLength); var second = Blend(first, _second, _signalLength);
        var smooth = _signalLength == 1 ? 0 : ExactMeanAccumulator.UnitRatio(2L * _signalLength * Units(first) - (_signalLength + 1L) * Units(second), new BigInteger(_signalLength - 1L));
        var value = ExactMeanAccumulator.UnitRatio(Units(residual) - Units(smooth), BigInteger.One);
        if (commit) { _gains = gains; _losses = losses; _ties = ties; _price = price; _mean = mean; _first = first; _second = second; _started = true; }
        return value;
    }
    internal void Reset() { _gains = _losses = _ties = default; _price = _mean = _first = _second = 0; _started = false; }
}
