using OoplesFinance.StockIndicators.Streaming;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class AnchoredMomentumWindow : IDisposable
{
    private readonly RocBankAverage? _smooth;
    private readonly IMovingAverageSmoother? _fallback;
    private readonly ExactPartialMeanWindow _anchor;
    private readonly PooledRingBuffer<RocBankValue> _signals;
    private ExactMeanAccumulator _sum;
    internal static int AnchorLength(int momentum)=>(int)Math.Max(2,Math.Min(530,2L*momentum+1));
    internal AnchoredMomentumWindow(MovingAvgType kind,int smooth,int signal,int momentum,int capacityHint=int.MaxValue)
    {
        smooth=Math.Max(1,smooth);signal=Math.Max(1,signal);
        if(StrengthWindow.Supports(kind))_smooth=new(kind,smooth,capacityHint);else _fallback=MovingAverageSmootherFactory.Create(kind,smooth);
        _anchor=new(AnchorLength(momentum));_signals=new(Math.Min(signal,Math.Max(1,capacityHint)));
    }
    internal (double Value,double Signal) Finish(double price,double smooth,bool commit)
    {
        var anchor=_anchor.Next(price,commit);var value=RocBankValue.Return(smooth,anchor);var sum=_sum;
        if(_signals.Count==_signals.Capacity)_signals[0].AddTo(ref sum,-1);value.AddTo(ref sum);
        var signal=RocBankValue.Round(sum,count:Math.Min(_signals.Count+1,_signals.Capacity));
        if(commit){_sum=sum;_signals.TryAdd(value,out _);}return(value.Publish(),signal.Publish());
    }
    internal (double Value,double Signal) Next(double price,bool commit)
    {var smooth=_smooth is not null?_smooth.Next(new RocBankValue(price),commit).Publish():_fallback!.Next(price,commit);return Finish(price,smooth,commit);}
    internal void Reset(){_smooth?.Reset();_fallback?.Reset();_anchor.Reset();_signals.Clear();_sum=default;}
    public void Dispose(){_smooth?.Dispose();_fallback?.Dispose();_anchor.Dispose();_signals.Dispose();}
}
