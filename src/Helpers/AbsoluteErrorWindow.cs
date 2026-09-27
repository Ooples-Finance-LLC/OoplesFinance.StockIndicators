namespace OoplesFinance.StockIndicators.Helpers;
// Cancel the common partial-window divisor before normalization; keep unpublished prediction stages extended.
internal sealed class AbsoluteErrorWindow
{
    private readonly int _length;
    private readonly Queue<RocBankValue> _errors=new();
    private ExactMeanAccumulator _net,_travel;
    private RocBankValue _prediction;
    private bool _started;
    internal AbsoluteErrorWindow(int length)=>_length=Math.Max(1,length);
    private static RocBankValue Abs(RocBankValue value)=>new(Math.Abs(value.Mantissa),value.UpperShift);
    internal double Next(double price,bool commit)
    {
        var previous=_started?_prediction:new RocBankValue(price);
        var delta=new ExactMeanAccumulator();delta.Add(price);previous.AddTo(ref delta,-1);var error=RocBankValue.Round(delta);
        var net=_net;var travel=_travel;
        if(_errors.Count==_length){var old=_errors.Peek();old.AddTo(ref net,-1);Abs(old).AddTo(ref travel,-1);}
        error.AddTo(ref net);Abs(error).AddTo(ref travel);
        var ratio=travel.IsExactlyZero?0:net.Ratio(travel);var count=_errors.Count<_length?_errors.Count+1:_length;
        var mean=RocBankValue.Round(travel,count:count);var correction=mean.Multiply(ratio);
        var predicted=new ExactMeanAccumulator();predicted.Add(price);correction.AddTo(ref predicted);var prediction=RocBankValue.Round(predicted);
        if(commit){if(_errors.Count==_length)_errors.Dequeue();_errors.Enqueue(error);_net=net;_travel=travel;_prediction=prediction;_started=true;}
        return ratio;
    }
    internal void Reset(){_errors.Clear();_net=_travel=default;_prediction=default;_started=false;}
}
