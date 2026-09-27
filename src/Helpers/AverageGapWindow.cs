using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AverageGapWindow : IDisposable
{
    private readonly RocBankAverage? _fast, _slow;
    private readonly IMovingAverageSmoother? _fastFallback, _slowFallback;
    internal AverageGapWindow(MovingAvgType kind,int fast,int slow,int capacityHint=int.MaxValue)
    {
        fast=Math.Max(1,fast);slow=Math.Max(1,slow);
        if(StrengthWindow.Supports(kind)){_fast=new(kind,fast,capacityHint);_slow=new(kind,slow,capacityHint);}
        else{_fastFallback=MovingAverageSmootherFactory.Create(kind,fast);_slowFallback=MovingAverageSmootherFactory.Create(kind,slow);}
    }
    internal static double Gap(double fast,double slow)=>RocBankValue.Return(fast,slow).Publish();
    internal double Next(double price,bool commit)
    {
        var fast=_fast is not null?_fast.Next(new RocBankValue(price),commit).Publish():_fastFallback!.Next(price,commit);
        var slow=_slow is not null?_slow.Next(new RocBankValue(price),commit).Publish():_slowFallback!.Next(price,commit);
        return Gap(fast,slow);
    }
    internal void Reset(){_fast?.Reset();_slow?.Reset();_fastFallback?.Reset();_slowFallback?.Reset();}
    public void Dispose(){_fast?.Dispose();_slow?.Dispose();_fastFallback?.Dispose();_slowFallback?.Dispose();}
}
