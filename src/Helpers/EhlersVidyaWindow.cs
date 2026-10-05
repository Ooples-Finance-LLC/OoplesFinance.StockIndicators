using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class EhlersVidyaWindow : IDisposable
{
    private readonly RocBankAverage _fast,_slow;
    private readonly int _fastPeriod,_slowPeriod;
    private readonly Queue<BigInteger> _fastSquares=new(),_slowSquares=new();
    private BigInteger _fastSum,_slowSum,_previousSlope;
    private double _previous;
    private bool _started;
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    internal EhlersVidyaWindow(MovingAvgType kind,int fastLength,int slowLength)
    {
        _fastPeriod=Math.Max(1,fastLength);_slowPeriod=Math.Max(1,slowLength);
        _fast=new(kind,_fastPeriod,1,true);_slow=new(kind,_slowPeriod,1,true);
    }
    internal (double Value,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));var value=U(price);
        var fastMean=_fast.Next(new RocBankValue(price),final).Publish();var slowMean=_slow.Next(new RocBankValue(price),final).Publish();
        var fastError=value-U(fastMean);var slowError=value-U(slowMean);var fastSquare=fastError*fastError;var slowSquare=slowError*slowError;
        var fastSum=_fastSum+fastSquare-(_fastSquares.Count==_fastPeriod?_fastSquares.Peek():BigInteger.Zero);
        var slowSum=_slowSum+slowSquare-(_slowSquares.Count==_slowPeriod?_slowSquares.Peek():BigInteger.Zero);
        var fastCount=Math.Min(_fastPeriod,_fastSquares.Count+1L);var slowCount=Math.Min(_slowPeriod,_slowSquares.Count+1L);
        var gain=0d;
        if(!slowSum.IsZero)
        {
            var multiplier=U(.2);var numerator=fastSum*slowCount*multiplier*multiplier;var denominator=slowSum*fastCount;
            var lower=U(.01);var upper=U(.99);
            gain=numerator<=denominator*lower*lower?.01:numerator>=denominator*upper*upper?.99:ExactPopulationDeviation.RootRatio(numerator,denominator);
        }
        var previous=_started?_previous:price;var blend=new ExactMeanAccumulator();blend.Add(previous);blend.AddProduct(price,gain);blend.AddProduct(previous,-gain);
        var result=blend.Mean(1);var slope=value-U(result);
        var trade=slope.Sign>0?slope>_previousSlope?Signal.StrongBuy:Signal.Buy:slope.Sign<0?slope<_previousSlope?Signal.StrongSell:Signal.Sell:Signal.None;
        if(final)
        {
            if(_fastSquares.Count==_fastPeriod)_fastSquares.Dequeue();_fastSquares.Enqueue(fastSquare);
            if(_slowSquares.Count==_slowPeriod)_slowSquares.Dequeue();_slowSquares.Enqueue(slowSquare);
            _fastSum=fastSum;_slowSum=slowSum;_previous=result;_previousSlope=slope;_started=true;
        }
        return(result,trade);
    }
    internal void Reset(){_fast.Reset();_slow.Reset();_fastSquares.Clear();_slowSquares.Clear();_fastSum=_slowSum=_previousSlope=default;_previous=0;_started=false;}
    public void Dispose(){_fast.Dispose();_slow.Dispose();_fastSquares.Clear();_slowSquares.Clear();}
    internal static (List<double> Values,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int fast,int slow)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);
        foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new EhlersVidyaWindow(kind,fast,slow);var output=new List<double>(input.Count);var signals=new List<Signal>(input.Count);
        foreach(var price in input){var point=state.Next(price,true);output.Add(point.Value);signals.Add(point.Trade);}return(output,signals);
    }
}
