namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class ProjectedLevelsWindow : IDisposable
{
    internal static readonly string[] Keys={"Support1","Support2","Resistance1","Resistance2","MiddleBand"};
    private readonly int _length;
    private readonly LinkedList<(long Index,double Value)> _highs=new(),_lows=new();
    private long _index;
    internal ProjectedLevelsWindow(int length)=>_length=Math.Max(1,length);
    private double Extreme(LinkedList<(long Index,double Value)> queue,double current,bool maximum,bool commit)
    {
        var first=queue.First;while(first is not null && first.Value.Index<=_index-_length)first=first.Next;
        var value=first is null?current:maximum?Math.Max(current,first.Value.Value):Math.Min(current,first.Value.Value);
        if(commit)
        {
            while(queue.First is not null && queue.First.Value.Index<=_index-_length)queue.RemoveFirst();
            while(queue.Last is not null && (maximum?queue.Last.Value.Value<=current:queue.Last.Value.Value>=current))queue.RemoveLast();
            queue.AddLast((_index,current));
        }
        return value;
    }
    private static RocBankValue Combine(RocBankValue a,RocBankValue b,int weight=1,int divisor=1)
    {
        var sum=new ExactMeanAccumulator();a.AddTo(ref sum);b.AddTo(ref sum,weight);return RocBankValue.Round(sum,count:divisor);
    }
    internal double[] Next(double high,double low,bool commit)
    {
        var h=Extreme(_highs,high,true,commit);var l=Extreme(_lows,low,false,commit);if(commit)_index++;
        var range=Combine(new(h),new(l),-1);var quarter=Combine(range,default,divisor:4);var half=Combine(range,default,divisor:2);
        var s1=Combine(new(l),quarter,-1);var s2=Combine(new(l),half,-1);var r1=Combine(new(h),quarter);var r2=Combine(new(h),half);
        var total=new ExactMeanAccumulator();s1.AddTo(ref total);s2.AddTo(ref total);r1.AddTo(ref total);r2.AddTo(ref total);
        return new[]{s1.Publish(),s2.Publish(),r1.Publish(),r2.Publish(),RocBankValue.Round(total,count:4).Publish()};
    }
    internal void Reset(){_highs.Clear();_lows.Clear();_index=0;}
    public void Dispose()=>Reset();
}
