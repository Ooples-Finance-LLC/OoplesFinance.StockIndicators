using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;

// Keep every rounded pressure, percentage, volume and ratio stage. In particular,
// subnormal sell volume may round to zero; cancelling volume changes that guard.
internal sealed class DemandIndexWindow
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private bool _seeded;
    private BigInteger _previous, _previousSlope;
    internal (double Value, Signal Signal) Next(double high, double low, double close, double volume, bool final)
    {
        var h = ExactVarianceWindow.Units(high); var l = ExactVarianceWindow.Units(low);
        var c = ExactVarianceWindow.Units(close); var v = ExactVarianceWindow.Units(volume);
        var line = BigInteger.Zero;
        if (_seeded)
        {
            var range = Round(h - l, BigInteger.One);
            var buying = Round(c - l, BigInteger.One);
            var selling = Round(h - c, BigInteger.One);
            var buyPercent = range.IsZero ? BigInteger.Zero : Round(buying * Unit, range);
            var sellPercent = range.IsZero ? BigInteger.Zero : Round(selling * Unit, range);
            var buyVolume = Round(v * buyPercent, Unit);
            var sellVolume = Round(v * sellPercent, Unit);
            if (!sellVolume.IsZero)
            {
                var ratio = Round(buyVolume * Unit, sellVolume);
                line = Round(ratio - Unit, BigInteger.One);
            }
        }
        var slope = line - _previous;
        var signal = slope.Sign > 0 && slope > _previousSlope ? Signal.StrongBuy : slope.Sign < 0 && slope < _previousSlope ? Signal.StrongSell
            : slope.Sign > 0 ? Signal.Buy : slope.Sign < 0 ? Signal.Sell : Signal.None;
        if (final) { _seeded = true; _previous = line; _previousSlope = slope; }
        return (ExactMeanAccumulator.UnitRatio(line, BigInteger.One), signal);
    }
    private static BigInteger Round(BigInteger numerator, BigInteger denominator) => denominator.Sign < 0
        ? RocBankValue.RoundUnits(-numerator, -denominator) : RocBankValue.RoundUnits(numerator, denominator);
    internal void Reset() { _seeded = false; _previous = _previousSlope = default; }
}
