using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VervoortVolatilityWindow : IDisposable
{
    private readonly RocBankAverage _first,_second,_width;
    private readonly Queue<RocBankValue> _means=new(),_ranges=new();
    private readonly int _meanPeriod,_rangePeriod;
    private readonly double _deviation,_lowerMultiplier;
    private ExactMeanAccumulator _meanSum,_rangeSum;
    private double _price,_low;
    private BigInteger _previousSlope,_previousUpper,_previousLower;
    private static BigInteger Units(RocBankValue value)=>ExactVarianceWindow.Units(value.Mantissa)<<value.UpperShift;
    internal VervoortVolatilityWindow(MovingAvgType kind,int length1,int length2,double devMult,double lowBandMult)
    {
        StreamingInputValidation.Finite(devMult,nameof(devMult));StreamingInputValidation.Finite(lowBandMult,nameof(lowBandMult));
        _meanPeriod=Math.Max(1,length1);_rangePeriod=Math.Max(1,length2);_deviation=devMult;_lowerMultiplier=lowBandMult;
        _first=new(kind,length1,1,true);_second=new(kind,length1,1,true);_width=new(kind,length1,1,true);
    }
    private static RocBankValue Combine(RocBankValue first,RocBankValue second,int secondWeight)
    {var sum=new ExactMeanAccumulator();first.AddTo(ref sum);second.AddTo(ref sum,secondWeight);return RocBankValue.Round(sum);}
    private static RocBankValue Observe(RocBankValue value,Queue<RocBankValue> history,int period,ref ExactMeanAccumulator committed,bool final)
    {
        var sum=committed;if(history.Count==period)history.Peek().AddTo(ref sum,-1);value.AddTo(ref sum);
        var result=RocBankValue.Round(sum,count:Math.Min(period,history.Count+1L));
        if(final){if(history.Count==period)history.Dequeue();history.Enqueue(value);committed=sum;}return result;
    }
    internal (double Upper,double Middle,double Lower,Signal Trade) Next(double price,double low,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));StreamingInputValidation.Finite(low,nameof(low));var value=new RocBankValue(price);
        var mean=_first.Next(value,final);var center=_second.Next(mean,final);var middle=Observe(mean,_means,_meanPeriod,ref _meanSum,final);
        var range=price>=_price?Combine(value,new RocBankValue(_low),-1):Combine(new RocBankValue(_price),new RocBankValue(low),-1);
        var typical=Observe(range,_ranges,_rangePeriod,ref _rangeSum,final);var width=_width.Next(typical.Multiply(_deviation),final);
        var upper=Combine(center,width,1);var lower=Combine(center,width.Multiply(_lowerMultiplier),-1);
        var priceUnits=ExactVarianceWindow.Units(price);var previousPrice=ExactVarianceWindow.Units(_price);var slope=priceUnits-Units(middle);
        var trade=slope.Sign>0 && slope>_previousSlope?Signal.StrongBuy:slope.Sign<0 && slope<_previousSlope?Signal.StrongSell
            :slope.Sign>0 || previousPrice<_previousLower && priceUnits>Units(lower)?Signal.Buy
            :slope.Sign<0 || previousPrice>_previousUpper && priceUnits<Units(upper)?Signal.Sell:Signal.None;
        if(final){_price=price;_low=low;_previousSlope=slope;_previousUpper=Units(upper);_previousLower=Units(lower);}
        return(upper.Publish(),middle.Publish(),lower.Publish(),trade);
    }
    internal void Reset(){_first.Reset();_second.Reset();_width.Reset();_means.Clear();_ranges.Clear();_meanSum=_rangeSum=default;_price=_low=0;_previousSlope=_previousUpper=_previousLower=default;}
    public void Dispose(){_first.Dispose();_second.Dispose();_width.Dispose();_means.Clear();_ranges.Clear();}
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int length1,int length2,double devMult,double lowBandMult)
    {
        var(input,_,low,_,_)=CalculationsHelper.GetInputValuesList(data);
        foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        var output=new Dictionary<string,List<double>>{{"UpperBand",new()},{"MiddleBand",new()},{"LowerBand",new()}};var signals=new List<Signal>(input.Count);
        using var state=new VervoortVolatilityWindow(kind,length1,length2,devMult,lowBandMult);
        for(var i=0;i<input.Count;i++){var point=state.Next(input[i],low[i],true);output["UpperBand"].Add(point.Upper);output["MiddleBand"].Add(point.Middle);output["LowerBand"].Add(point.Lower);signals.Add(point.Trade);}return(output,signals);
    }
}
