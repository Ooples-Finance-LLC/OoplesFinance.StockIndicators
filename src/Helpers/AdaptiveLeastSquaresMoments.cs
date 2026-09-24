namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>Centered weighted least squares with adaptive exponential forgetting.</summary>
internal struct AdaptiveLeastSquaresMoments
{
    private bool _initialized;
    private double _meanAge;
    private double _meanPrice;
    private double _ageVariance;
    private double _covariance;

    internal double Next(double price, double gain)
    {
        if (!_initialized)
        {
            _initialized = true;
            _meanPrice = price;
            return price;
        }
        // Shift existing observations back one bar; the new observation has age zero.
        // Centered moments avoid subtracting large squared bar indices or price levels.
        var ageDifference = 1 - _meanAge;
        var priceDifference = price - _meanPrice;
        var retention = 1 - gain;
        _ageVariance = retention * (_ageVariance + gain * ageDifference * ageDifference);
        _covariance = retention * (_covariance + gain * ageDifference * priceDifference);
        _meanAge = retention * (_meanAge - 1);
        _meanPrice += gain * priceDifference;
        return _ageVariance == 0 ? _meanPrice : _meanPrice - _meanAge * (_covariance / _ageVariance);
    }
}
