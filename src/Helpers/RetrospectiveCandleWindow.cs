using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class RetrospectiveCandleWindow
{
    private readonly int _length;
    private readonly LinkedList<(long Index,BigInteger Value)> _maximum=new(),_minimum=new();
    private long _count;
    private double _previousPrice,_previousClose;
    internal RetrospectiveCandleWindow(int length)=>_length=Math.Max(1,length);
    private static double Blend(double previous,double current,double weight)
    {
        var sum=new ExactMeanAccumulator();sum.Add(previous);sum.AddProduct(current,weight);sum.AddProduct(previous,weight,-1);return sum.Mean(1);
    }
    private void Commit(LinkedList<(long Index,BigInteger Value)> values,BigInteger current,bool maximum)
    {
        while(values.First is not null&&values.First.Value.Index<=_count-_length)values.RemoveFirst();
        while(values.Last is not null&&(maximum?values.Last.Value.Value<=current:values.Last.Value.Value>=current))values.RemoveLast();
        values.AddLast((_count,current));
    }
    internal double Next(double open,double high,double low,double close,bool commit)
    {
        var change=BigInteger.Abs(RocBankValue.RoundUnits(ExactVarianceWindow.Units(close)-ExactVarianceWindow.Units(_previousPrice),BigInteger.One));
        var maxNode=_maximum.First;while(maxNode is not null&&maxNode.Value.Index<=_count-_length)maxNode=maxNode.Next;
        var minNode=_minimum.First;while(minNode is not null&&minNode.Value.Index<=_count-_length)minNode=minNode.Next;
        var highest=maxNode is null?change:BigInteger.Max(change,maxNode.Value.Value);var lowest=minNode is null?change:BigInteger.Min(change,minNode.Value.Value);
        var range=highest-lowest;var ratio=range.IsZero?0:ExactMeanAccumulator.UnitRatio((change-lowest)<<1074,range);
        var weight=(ratio*100)/100;
        var previous=_count==0?close:_previousClose;var c=Blend(previous,close,weight);
        var h=Blend(_count==0?high:previous,high,weight);var l=Blend(_count==0?low:previous,low,weight);var o=Blend(_count==0?open:previous,open,weight);
        var mean=new ExactMeanAccumulator();mean.Add(c);mean.Add(h);mean.Add(l);mean.Add(o);var result=mean.Mean(4);
        if(commit){Commit(_maximum,change,true);Commit(_minimum,change,false);_count++;_previousPrice=close;_previousClose=c;}
        return result;
    }
    internal void Reset(){_maximum.Clear();_minimum.Clear();_count=0;_previousPrice=_previousClose=0;}
}
