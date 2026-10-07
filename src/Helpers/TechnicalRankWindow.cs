using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class TechnicalRankWindow : IDisposable
{
    private static readonly BigInteger Unit = BigInteger.One << 1074;
    private readonly int _longPeriod, _mediumPeriod, _slopePeriod;
    private readonly RocBankAverage _longMean, _mediumMean, _fast, _slow, _signal;
    private readonly PriceRsiWindow _rsi;
    private readonly Queue<double> _longPrices = new(), _mediumPrices = new();
    private readonly Queue<BigInteger> _histograms = new();
    private BigInteger _previous, _previousSlope;
    internal TechnicalRankWindow(int length1,int length2,int length3,int length4,int length5,int length6,int length7,int length8,int length9)
    {
        _longPeriod=Math.Max(1,length2);_mediumPeriod=Math.Max(1,length4);_slopePeriod=Math.Max(1,length8);
        _longMean=new(MovingAvgType.SimpleMovingAverage,length1,1,true);_mediumMean=new(MovingAvgType.SimpleMovingAverage,length3,1,true);
        _fast=new(MovingAvgType.ExponentialMovingAverage,length5,1,true);_slow=new(MovingAvgType.ExponentialMovingAverage,length6,1,true);
        _signal=new(MovingAvgType.ExponentialMovingAverage,length7,1,true);_rsi=new(MovingAvgType.WildersSmoothingMethod,Math.Max(1,length9),1);
    }
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger numerator,BigInteger denominator)=>RocBankValue.RoundUnits(denominator.Sign<0?-numerator:numerator,BigInteger.Abs(denominator));
    private static BigInteger Round(BigInteger value)=>Round(value,BigInteger.One);
    private static BigInteger Average(RocBankAverage average,BigInteger value,bool final)
    {
        var sum=new ExactMeanAccumulator();sum.Add(double.Epsilon,value);var result=average.Next(RocBankValue.Round(sum),final);
        return U(result.Mantissa)<<result.UpperShift;
    }
    private static BigInteger Percent(BigInteger value,BigInteger basis)=>basis.IsZero?BigInteger.Zero:Round(100*(value-basis)<<1074,basis);
    private static BigInteger MeanLeg(BigInteger price,BigInteger mean,int weight)=>mean.IsZero?BigInteger.Zero:Round(Round(weight*Round(price-mean))<<1074,mean);
    internal (double Value,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));var value=U(price);
        var longMean=Average(_longMean,value,final);var mediumMean=Average(_mediumMean,value,final);
        var longRoc=_longPrices.Count==_longPeriod?Percent(value,U(_longPrices.Peek())):BigInteger.Zero;
        var mediumRoc=_mediumPrices.Count==_mediumPeriod?Percent(value,U(_mediumPrices.Peek())):BigInteger.Zero;
        var fast=Average(_fast,value,final);var slow=Average(_slow,value,final);var ppo=Percent(fast,slow);
        var signal=Average(_signal,ppo,final);var histogram=Round(ppo-signal);
        var slope=_histograms.Count==_slopePeriod?Round(Round(histogram-_histograms.Peek()),_slopePeriod):BigInteger.Zero;
        var strength=_rsi.Next(price,final);
        var longMa=MeanLeg(value,longMean,30);var longReturn=Round(U(.3)*longRoc,Unit);
        var mediumMa=MeanLeg(value,mediumMean,15);var mediumReturn=Round(U(.15)*mediumRoc,Unit);
        var impulse=Round(5*slope);var shortRsi=Round(U(.05)*U(strength),Unit);
        var rank=Round(Round(Round(Round(Round(longMa+longReturn)+mediumMa)+mediumReturn)+impulse)+shortRsi);
        rank=BigInteger.Min(100*Unit,BigInteger.Max(BigInteger.Zero,rank));
        var change=rank-_previous;
        var trade=change.Sign>0?change>_previousSlope?Signal.StrongBuy:Signal.Buy
            :change.Sign<0?change<_previousSlope?Signal.StrongSell:Signal.Sell:Signal.None;
        if(final)
        {
            if(_longPrices.Count==_longPeriod)_longPrices.Dequeue();_longPrices.Enqueue(price);
            if(_mediumPrices.Count==_mediumPeriod)_mediumPrices.Dequeue();_mediumPrices.Enqueue(price);
            if(_histograms.Count==_slopePeriod)_histograms.Dequeue();_histograms.Enqueue(histogram);
            _previous=rank;_previousSlope=change;
        }
        return(ExactMeanAccumulator.UnitRatio(rank,BigInteger.One),trade);
    }
    internal void Reset()
    {
        _longMean.Reset();_mediumMean.Reset();_fast.Reset();_slow.Reset();_signal.Reset();_rsi.Reset();
        _longPrices.Clear();_mediumPrices.Clear();_histograms.Clear();_previous=_previousSlope=default;
    }
    public void Dispose(){_longMean.Dispose();_mediumMean.Dispose();_fast.Dispose();_slow.Dispose();_signal.Dispose();_rsi.Dispose();_longPrices.Clear();_mediumPrices.Clear();_histograms.Clear();}
    internal static (List<double> Values,List<Signal> Signals) Calculate(StockData data,int l1,int l2,int l3,int l4,int l5,int l6,int l7,int l8,int l9)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);
        foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})
            foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new TechnicalRankWindow(l1,l2,l3,l4,l5,l6,l7,l8,l9);var output=new List<double>(input.Count);var signals=new List<Signal>(input.Count);
        foreach(var value in input){var point=state.Next(value,true);output.Add(point.Value);signals.Add(point.Trade);}
        return(output,signals);
    }
}
