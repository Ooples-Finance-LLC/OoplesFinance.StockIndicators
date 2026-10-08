using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DistanceCoefficientWindow
{
    private readonly int _length;
    private readonly Queue<BigInteger> _prices = new();
    private readonly Queue<(double Price, BigInteger Weight)> _terms = new();
    private BigInteger _priceSum, _priceSquares, _mass;
    private ExactMeanAccumulator _numerator;
    internal DistanceCoefficientWindow(int length) => _length = Math.Max(1,length);
    internal double Next(double price,bool commit)
    {
        var units = ExactVarianceWindow.Units(price);
        // Sum of squared distances to the preceding length-1 observations, including unseen zeros.
        var weight = (_length-1L)*units*units - 2*units*_priceSum + _priceSquares;
        var numerator = _numerator; var mass = _mass;
        if(_terms.Count==_length){var oldest=_terms.Peek();numerator.Add(oldest.Price,-oldest.Weight);mass-=oldest.Weight;}
        numerator.Add(price,weight);mass+=weight;var denominator=new ExactMeanAccumulator();denominator.Add(1d,mass);
        var result=mass.IsZero?price:numerator.Ratio(denominator);
        if(commit)
        {
            if(_length>1)
            {
                if(_prices.Count==_length-1){var oldest=_prices.Dequeue();_priceSum-=oldest;_priceSquares-=oldest*oldest;}
                _prices.Enqueue(units);_priceSum+=units;_priceSquares+=units*units;
            }
            if(_terms.Count==_length)_terms.Dequeue();_terms.Enqueue((price,weight));_numerator=numerator;_mass=mass;
        }
        return result;
    }
    internal void Reset(){_prices.Clear();_terms.Clear();_priceSum=_priceSquares=_mass=default;_numerator=default;}
}
