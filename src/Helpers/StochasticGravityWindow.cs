using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class StochasticGravityWindow : IDisposable
{
    private readonly CenterGravityWindow _gravity;
    private readonly int _length;
    private readonly LinkedList<(long Index,BigInteger Value)> _minimum=new(),_maximum=new();
    private readonly PooledRingBuffer<double> _ratios=new(3);
    private long _index;
    private double _previous;
    internal StochasticGravityWindow(int length,int capacityHint=int.MaxValue){_gravity=new(length,capacityHint);_length=Math.Max(2,length);}
    private BigInteger Extreme(LinkedList<(long Index,BigInteger Value)> queue,BigInteger current,bool maximum)
    {
        var node=queue.First;while(node is not null&&node.Value.Index<=_index-_length)node=node.Next;
        if(node is null)return current;return maximum?BigInteger.Max(current,node.Value.Value):BigInteger.Min(current,node.Value.Value);
    }
    private void Commit(LinkedList<(long Index,BigInteger Value)> queue,BigInteger current,bool maximum)
    {
        while(queue.First is not null&&queue.First.Value.Index<=_index-_length)queue.RemoveFirst();
        while(queue.Last is not null&&(maximum?queue.Last.Value.Value<=current:queue.Last.Value.Value>=current))queue.RemoveLast();
        queue.AddLast((_index,current));
    }
    internal double Next(double price,bool commit)
    {
        var gravity=_gravity.NextUnits(price,commit);var min=Extreme(_minimum,gravity,false);var max=Extreme(_maximum,gravity,true);
        var ratio=max==min?0:ExactMeanAccumulator.UnitRatio((gravity-min)<<1074,max-min);
        var sum=new ExactMeanAccumulator();sum.Add(ratio,4);for(var lag=1;lag<=3&&lag<=_ratios.Count;lag++)sum.Add(_ratios[_ratios.Count-lag],4-lag);
        var smoothed=sum.Mean(10);var centered=2*(smoothed-.5);var trigger=Math.Max(0,Math.Min(1,.96*(_previous+.02)));
        if(commit){Commit(_minimum,gravity,false);Commit(_maximum,gravity,true);_ratios.TryAdd(ratio,out _);_previous=centered;_index++;}
        return trigger;
    }
    internal void Reset(){_gravity.Reset();_minimum.Clear();_maximum.Clear();_ratios.Clear();_previous=0;_index=0;}
    public void Dispose(){_gravity.Dispose();_ratios.Dispose();_minimum.Clear();_maximum.Clear();}
}
