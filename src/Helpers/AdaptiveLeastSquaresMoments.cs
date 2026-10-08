using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
// Rounded centered moments with extended exponents for unpublished price terms.
internal struct AdaptiveLeastSquaresMoments
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private bool _initialized;
    private BigInteger _meanAge, _meanPrice, _ageVariance, _covariance;
    internal BigInteger ExactOutput { get; private set; }
    internal double Next(double price, double gain)
    {
        var input = ExactVarianceWindow.Units(price);
        if (!_initialized) { _initialized = true; _meanPrice = ExactOutput = input; return price; }
        var weight = ExactVarianceWindow.Units(gain); var retention = ExactVarianceWindow.Units(1 - gain);
        var ageDifference = Unit - _meanAge; var priceDifference = input - _meanPrice;
        _ageVariance = RocBankValue.RoundUnits(retention * ((_ageVariance << 2148) + weight * ageDifference * ageDifference), BigInteger.One << 3222);
        _covariance = RocBankValue.RoundUnits(retention * ((_covariance << 2148) + weight * ageDifference * priceDifference), BigInteger.One << 3222);
        _meanAge = RocBankValue.RoundUnits(retention * (_meanAge - Unit), Unit);
        _meanPrice = RocBankValue.RoundUnits((_meanPrice << 1074) + weight * priceDifference, Unit);
        ExactOutput = _ageVariance.IsZero ? _meanPrice : RocBankValue.RoundUnits(_meanPrice * _ageVariance - _meanAge * _covariance, _ageVariance);
        return ExactMeanAccumulator.UnitRatio(ExactOutput, BigInteger.One);
    }
}
