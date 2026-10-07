namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class SimplePriceZoneWindow
{
    private readonly int _length;
    private readonly Queue<RocBankValue> _changes=new();
    private ExactMeanAccumulator _net,_travel;
    private double _previous;
    private bool _started;
    internal SimplePriceZoneWindow(int length)=>_length=Math.Max(1,length);
    private static RocBankValue Abs(RocBankValue value)=>new(Math.Abs(value.Mantissa),value.UpperShift);
    internal double Next(double price,bool commit)
    {
        var delta=new ExactMeanAccumulator();if(_started){delta.Add(price);delta.Add(_previous,-1);}var change=RocBankValue.Round(delta);
        var net=_net;var travel=_travel;
        if(_changes.Count==_length){var old=_changes.Peek();old.AddTo(ref net,-100);Abs(old).AddTo(ref travel,-1);}
        change.AddTo(ref net,100);Abs(change).AddTo(ref travel);
        var result=travel.IsExactlyZero?0:net.Ratio(travel);
        if(commit){if(_changes.Count==_length)_changes.Dequeue();_changes.Enqueue(change);_net=net;_travel=travel;_previous=price;_started=true;}
        return result;
    }
    internal void Reset(){_changes.Clear();_net=_travel=default;_previous=0;_started=false;}
}
