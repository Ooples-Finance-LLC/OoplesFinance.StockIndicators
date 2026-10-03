using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AutoDriftWindow
{
    private readonly int _length;
    private readonly double _gain;
    private readonly Queue<BigInteger> _prices=new();
    private readonly Queue<RocBankValue> _lines=new();
    private BigInteger _sum,_squares;
    private RocBankValue _previous;
    private bool _started;
    internal AutoDriftWindow(int length){_length=Math.Max(1,length);_gain=1d/(2L*_length);}
    private static RocBankValue Combine(RocBankValue a,RocBankValue b,int sign=1)
    {var sum=new ExactMeanAccumulator();a.AddTo(ref sum);b.AddTo(ref sum,sign);return RocBankValue.Round(sum);}
    internal double Next(double price,bool commit)
    {
        var current=ExactVarianceWindow.Units(price);var sum=_sum+current;var squares=_squares+current*current;
        if(_prices.Count==_length){var old=_prices.Peek();sum-=old;squares-=old*old;}
        var deviation=_prices.Count<_length-1?0:ExactPopulationDeviation.RootRatio(_length*squares-sum*sum,(BigInteger)_length*_length);
        var seed=new RocBankValue(Math.Round(price));var previous=_started?_previous:seed;var prior=_lines.Count>_length?_lines.Peek():seed;
        var upper=Combine(previous,new RocBankValue(deviation));var lower=Combine(previous,new RocBankValue(deviation),-1);
        var drift=Combine(previous,prior,-1).Multiply(_gain);
        var line=price>upper.Publish()||price<lower.Publish()?new RocBankValue(price):Combine(previous,drift);
        if(commit)
        {
            if(_prices.Count==_length)_prices.Dequeue();_prices.Enqueue(current);_sum=sum;_squares=squares;
            if(_lines.Count>_length)_lines.Dequeue();_lines.Enqueue(line);_previous=line;_started=true;
        }
        return line.Publish();
    }
    internal void Reset(){_prices.Clear();_lines.Clear();_sum=_squares=default;_previous=default;_started=false;}
}
