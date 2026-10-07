using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class MassIndexSum
{
    private readonly int _length;
    private readonly Queue<RocBankValue> _ratios = new();
    private ExactMeanAccumulator _sum;
    internal MassIndexSum(int length) => _length = Math.Max(1,length);
    internal static RocBankValue Ratio(RocBankValue first,RocBankValue second)
    {
        if(second.Mantissa==0)return default;
        var numerator=new ExactMeanAccumulator();first.AddTo(ref numerator);
        var denominator=new ExactMeanAccumulator();second.AddTo(ref denominator);
        for(var shift=0;;shift+=1024)
        {
            var scaled=denominator;scaled.ScaleByPowerOfTwo(shift);var value=numerator.Ratio(scaled);
            if(!double.IsInfinity(value))return new(value,shift);
        }
    }
    internal RocBankValue Next(RocBankValue first,RocBankValue second,bool commit)
    {
        var ratio=Ratio(first,second);var sum=_sum;
        if(_ratios.Count==_length)_ratios.Peek().AddTo(ref sum,-1);
        ratio.AddTo(ref sum);
        if(commit){_sum=sum;if(_ratios.Count==_length)_ratios.Dequeue();_ratios.Enqueue(ratio);}
        return RocBankValue.Round(sum);
    }
    internal void Reset(){_ratios.Clear();_sum=default;}
}
internal sealed class MassIndexWindow : IDisposable
{
    private readonly RocBankAverage[]? _exact;
    private readonly IMovingAverageSmoother[]? _fallback;
    private readonly MassIndexSum _sum;
    internal MassIndexWindow(MovingAvgType kind,int first,int second,int length,int signal,int capacityHint=int.MaxValue)
    {
        var periods=new[]{Math.Max(1,first),Math.Max(1,second),Math.Max(1,signal)};
        if(StrengthWindow.Supports(kind))_exact=periods.Select(p=>new RocBankAverage(kind,p,capacityHint)).ToArray();
        else _fallback=periods.Select(p=>MovingAverageSmootherFactory.Create(kind,p)).ToArray();
        _sum=new(length);
    }
    internal static RocBankValue Range(double high,double low)
    {
        var range=new ExactMeanAccumulator();range.Add(high);range.Add(low,-1);return RocBankValue.Round(range);
    }
    private RocBankValue Average(int stage,RocBankValue value,bool commit)
        =>_exact is not null?_exact[stage].Next(value,commit):new(_fallback![stage].Next(value.Publish(),commit));
    internal (double Value,double Signal) Next(double high,double low,bool commit)
    {
        var first=Average(0,Range(high,low),commit);var second=Average(1,first,commit);
        var value=_sum.Next(first,second,commit);var signal=Average(2,value,commit);
        return(value.Publish(),signal.Publish());
    }
    internal void Reset(){if(_exact is not null)foreach(var average in _exact)average.Reset();if(_fallback is not null)foreach(var average in _fallback)average.Reset();_sum.Reset();}
    public void Dispose(){if(_exact is not null)foreach(var average in _exact)average.Dispose();if(_fallback is not null)foreach(var average in _fallback)average.Dispose();_sum.Reset();}
}
