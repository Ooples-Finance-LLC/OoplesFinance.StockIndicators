using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TradersDynamicWindow : IDisposable
{
    private readonly RocBankAverage _gains, _losses, _center, _fast, _slow;
    private readonly int _period;
    private readonly bool _carryFlat;
    private readonly Queue<double> _history = new();
    private bool _started;
    private double _price, _rsi, _previousFast, _previousUpper, _previousLower;
    private BigInteger _previousSlope;
    internal TradersDynamicWindow(MovingAvgType kind,int length1,int length2,int length3,int length4)
    {
        _period=Math.Max(1,length2);_carryFlat=length1>1 && kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod;
        _gains=new(kind,length1,1,true);_losses=new(kind,length1,1,true);
        _center=new(kind,length2,1,true);_fast=new(kind,length3,1,true);_slow=new(kind,length4,1,true);
    }
    private double Deviation(double current)
    {
        if(_history.Count<_period-1)return 0;
        var values=_history.Count==_period?_history.Skip(1).Concat(new[] { current }):_history.Concat(new[] { current });
        var sample=values.ToArray();var anchor=sample[0];double sum=0;
        foreach(var value in sample)sum+=value-anchor;
        var mean=sum/_period;double variance=0;var lost=false;
        foreach(var value in sample){var delta=(value-anchor)-mean;var square=delta*delta;lost|=delta!=0 && square<2.2250738585072014E-308;variance+=square;}
        variance/=_period;
        if(lost || variance>0 && variance<2.2250738585072014E-308)
        {var exact=new ExactPopulationDeviation();foreach(var value in sample)exact.Add(value);return exact.Value();}
        return Math.Sqrt(variance);
    }
    internal (double Line,double Signal,double Upper,double Middle,double Lower,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));
        var difference=new ExactMeanAccumulator();if(_started){difference.Add(price);difference.Add(_price,-1);}
        var change=RocBankValue.Round(difference);
        var gain=_gains.Next(change.Mantissa>0?change:default,final);
        var loss=_losses.Next(change.Mantissa<0?new RocBankValue(-change.Mantissa,change.UpperShift):default,final);
        var numerator=new ExactMeanAccumulator();gain.AddTo(ref numerator,100);
        var total=new ExactMeanAccumulator();gain.AddTo(ref total);loss.AddTo(ref total);
#pragma warning disable S1244 // Flat-price carry applies only to exactly identical prices.
        var rsi=_carryFlat && _started && price==_price?_rsi:total.IsExactlyZero?100:numerator.Ratio(total);
#pragma warning restore S1244
        var value=new RocBankValue(rsi);var center=_center.Next(value,final).Publish();
        var fast=_fast.Next(value,final).Publish();var slow=_slow.Next(value,final).Publish();
        var offset=1.6185*Deviation(rsi);var upper=center+offset;var lower=center-offset;var middle=(upper+lower)/2;
        var slope=ExactVarianceWindow.Units(fast)-ExactVarianceWindow.Units(slow);
        var trade=slope.Sign>0 && slope>_previousSlope?Signal.StrongBuy:slope.Sign<0 && slope<_previousSlope?Signal.StrongSell
            :slope.Sign>0 || _previousFast<_previousLower && fast>lower?Signal.Buy
            :slope.Sign<0 || _previousFast>_previousUpper && fast<upper?Signal.Sell:Signal.None;
        if(final)
        {
            if(_history.Count==_period)_history.Dequeue();_history.Enqueue(rsi);
            _started=true;_price=price;_rsi=rsi;_previousFast=fast;_previousUpper=upper;_previousLower=lower;_previousSlope=slope;
        }
        return(fast,slow,upper,middle,lower,trade);
    }
    internal void Reset()
    {
        _gains.Reset();_losses.Reset();_center.Reset();_fast.Reset();_slow.Reset();_history.Clear();
        _started=false;_price=_rsi=_previousFast=_previousUpper=_previousLower=0;_previousSlope=default;
    }
    public void Dispose(){_gains.Dispose();_losses.Dispose();_center.Dispose();_fast.Dispose();_slow.Dispose();_history.Clear();}
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int l1,int l2,int l3,int l4)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);
        foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        var output=new Dictionary<string,List<double>>{{"UpperBand",new()},{"MiddleBand",new()},{"LowerBand",new()},{"Tdi",new()},{"Signal",new()}};var signals=new List<Signal>(input.Count);
        using var state=new TradersDynamicWindow(kind,l1,l2,l3,l4);
        foreach(var value in input)
        {var point=state.Next(value,true);output["UpperBand"].Add(point.Upper);output["MiddleBand"].Add(point.Middle);output["LowerBand"].Add(point.Lower);output["Tdi"].Add(point.Line);output["Signal"].Add(point.Signal);signals.Add(point.Trade);}
        return(output,signals);
    }
}
