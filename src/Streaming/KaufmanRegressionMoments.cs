using System;

namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Price-adaptive, consistently weighted centered regression moments.</summary>
internal sealed class KaufmanRegressionMoments : IDisposable
{
    private readonly EfficiencyRatioState _efficiency;
    private readonly int _length;
    private int _count;
    private Moments _moments;

    private struct Moments
    {
        internal double Age, Price, TimeVariance, PriceVariance, Covariance;
    }

    internal KaufmanRegressionMoments(int length)
    {
        _length = Math.Max(1, length);
        _efficiency = new EfficiencyRatioState(_length);
    }

    internal double Next(double price, bool isFinal, out double indexDeviation,
        out double sourceDeviation, out double correlation)
    {
        var efficiency = _efficiency.Next(price, isFinal);
        var gain = _count < _length ? 1 : Math.Pow(2d/31 + efficiency*(2d/3 - 2d/31), 2);
        var next = _moments;
        if (gain == 1)
            next = new Moments { Price = price };
        else
        {
            // Old samples move back one bar; the new observation has age zero.
            var dx = 1 - next.Age;
            var dy = price - next.Price;
            var retained = 1 - gain;
            next.TimeVariance = retained*(next.TimeVariance + gain*dx*dx);
            next.PriceVariance = retained*(next.PriceVariance + gain*dy*dy);
            next.Covariance = retained*(next.Covariance + gain*dx*dy);
            next.Age = retained*(next.Age - 1);
            next.Price += gain*dy;
        }
        indexDeviation = Math.Sqrt(next.TimeVariance);
        sourceDeviation = Math.Sqrt(next.PriceVariance);
        correlation = indexDeviation == 0 || sourceDeviation == 0 ? 0
            : Math.Max(-1, Math.Min(1, next.Covariance/indexDeviation/sourceDeviation));
        var fit = next.TimeVariance == 0 ? next.Price
            : next.Price - next.Age*(next.Covariance/next.TimeVariance);
        if (isFinal)
        {
            _moments = next;
            if (_count < _length) _count++;
        }
        return fit;
    }

    internal void Reset()
    {
        _efficiency.Reset();
        _count = 0;
        _moments = default;
    }

    public void Dispose() => _efficiency.Dispose();
}
