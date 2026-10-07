namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class ChaikinFlowWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<(RocBankValue Flow,double Volume)> _history=new();
    private ExactMeanAccumulator _flow,_volume;
    internal ChaikinFlowWindow(int length)=>_length=Math.Max(1,length);
    internal double Next(double high,double low,double close,double volume,bool commit)
    {
        var current=MoneyFlowAccumulationWindow.Flow(high,low,close,volume);
        var flowSum=_flow;var volumeSum=_volume;
        if(_history.Count==_length){var old=_history.Peek();old.Flow.AddTo(ref flowSum,-1);volumeSum.Add(old.Volume,-1);}
        current.AddTo(ref flowSum);volumeSum.Add(volume);
        var value=volumeSum.IsExactlyZero?0:flowSum.Ratio(volumeSum);
        if(commit){_flow=flowSum;_volume=volumeSum;if(_history.Count==_length)_history.Dequeue();_history.Enqueue((current,volume));}
        return value;
    }
    internal void Reset(){_history.Clear();_flow=_volume=default;}
    public void Dispose()=>_history.Clear();
}
