namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TironeWindow : IDisposable
{
    internal static readonly string[] Keys={"Tlh","Clh","Blh","Am","Eh","El","Rh","Rl"};
    private readonly int _length;
    private readonly LinkedList<(long Index,double Value)> _highs=new(),_lows=new();
    private long _index;
    internal TironeWindow(int length)=>_length=Math.Max(1,length);
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
    internal double[] Next(double high,double low,double close,bool commit)
    {
        var outputs = new double[8];
        Next(high, low, close, commit, outputs);
        return outputs;
    }
    internal void Next(double high, double low, double close, bool commit, Span<double> outputs)
    {
        if (outputs.Length < 8) throw new ArgumentException("Eight output slots are required.", nameof(outputs));
        var h=Extreme(_highs,high,true,commit);var l=Extreme(_lows,low,false,commit);if(commit)_index++;
        var range=Combine(new(h),new(l),-1);var third=Combine(range,default,divisor:3);var half=Combine(range,default,divisor:2);
        var total=new ExactMeanAccumulator();total.Add(h);total.Add(l);total.Add(close);var mean=RocBankValue.Round(total,count:3);
        var twice=new ExactMeanAccumulator();mean.AddTo(ref twice,2);var upper=twice;upper.Add(l,-1);var lower=twice;lower.Add(h,-1);
        outputs[0] = Combine(new(h),third,-1).Publish();
        outputs[1] = Combine(new(l),half).Publish();
        outputs[2] = Combine(new(l),third).Publish();
        outputs[3] = mean.Publish();
        outputs[4] = Combine(mean,range).Publish();
        outputs[5] = Combine(mean,range,-1).Publish();
        outputs[6] = RocBankValue.Round(upper).Publish();
        outputs[7] = RocBankValue.Round(lower).Publish();
    }
    internal void Reset(){_highs.Clear();_lows.Clear();_index=0;}
    public void Dispose()=>Reset();
}
