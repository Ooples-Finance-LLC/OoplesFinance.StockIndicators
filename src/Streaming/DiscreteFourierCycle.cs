using System.Numerics;
namespace OoplesFinance.StockIndicators.Streaming;

// Exact sums over observed history; no period-by-period history matrix.
internal sealed class DiscreteFourierCycle : IDisposable
{
    private readonly int _minimum,_maximum;private readonly double _pole,_drive;
    private readonly Queue<BigInteger> _hp=new(),_cleaned=new(),_prices=new();private BigInteger _previousInput;private int _count;
    internal Signal LastSignal { get; private set; }
    internal DiscreteFourierCycle(int minimum,int maximum,int cutoff)
    {
        _minimum=Math.Max(3,minimum);_maximum=Math.Max(_minimum,maximum);var angle=Math.Max(.01,Math.Min(.99,2*Math.PI/Math.Max(1,cutoff)));_pole=Math.Cos(angle)/(1+Math.Sin(angle));_drive=(1+_pole)/2;
    }
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    internal double Next(double input,bool final,out double highPass)
    {
        StreamingInputValidation.Finite(input,nameof(input));var price=U(input);var history=_hp.ToArray();var previous=history.Length==0?BigInteger.Zero:history[history.Length-1];
        var hp=_count<6?price:RocBankValue.RoundUnits(previous*U(_pole)+(price-_previousInput)*U(_drive),BigInteger.One<<1074);var cleaned=hp;
        if(_count>=6){for(var lag=1;lag<=5;lag++)cleaned+=history[history.Length-lag]*(lag is 2 or 3?3:lag is 1 or 4?2:1);cleaned=RocBankValue.RoundUnits(cleaned,12);}
        var kept=_cleaned.Count==_maximum?_cleaned.Skip(1).ToArray():_cleaned.ToArray();var samples=new BigInteger[kept.Length+1];samples[0]=cleaned;for(var i=0;i<kept.Length;i++)samples[i+1]=kept[kept.Length-1-i];
        var mass=samples.Select(BigInteger.Abs).Max();var scale=BigInteger.Abs(price);foreach(var old in _prices.Count==_maximum?_prices.Skip(1):_prices)scale=BigInteger.Max(scale,BigInteger.Abs(old));
        var numerator=new ExactMeanAccumulator();var denominator=new ExactMeanAccumulator();
        if((mass<<1074)>scale*U(1.4210854715202004e-14))
        {
            var spectrum=new List<(int Period,BigInteger Power)>();BigInteger peak=0;
            for(long period=_minimum;period<=_maximum;period++)
            {
                BigInteger real=0,imaginary=0;for(var lag=0;lag<samples.Length;lag++){var angle=2*Math.PI*lag/period;real+=samples[lag]*U(Math.Cos(angle));imaginary+=samples[lag]*U(Math.Sin(angle));}
                var power=real*real+imaginary*imaginary;spectrum.Add(((int)period,power));peak=BigInteger.Max(peak,power);
            }
            if(!peak.IsZero)foreach(var bin in spectrum)
            {
                var argument=ExactMeanAccumulator.UnitRatio((100*peak-99*bin.Power)<<1074,peak);var weight=Math.Max(0,3-10*Math.Log10(argument));numerator.Add(weight,bin.Period);denominator.Add(weight);
            }
        }
        var result=denominator.IsExactlyZero?0:numerator.Ratio(denominator);LastSignal=hp.Sign>0&&hp>previous?Signal.StrongBuy:hp.Sign<0&&hp<previous?Signal.StrongSell:hp.Sign>0?Signal.Buy:hp.Sign<0?Signal.Sell:Signal.None;
        if(final){if(_hp.Count==5)_hp.Dequeue();_hp.Enqueue(hp);if(_cleaned.Count==_maximum)_cleaned.Dequeue();_cleaned.Enqueue(cleaned);if(_prices.Count==_maximum)_prices.Dequeue();_prices.Enqueue(price);_previousInput=price;if(_count<6)_count++;}
        highPass=ExactMeanAccumulator.UnitRatio(hp,BigInteger.One);return result;
    }
    internal void Reset(){_hp.Clear();_cleaned.Clear();_prices.Clear();_previousInput=default;_count=0;LastSignal=Signal.None;}
    public void Dispose()=>Reset();
    internal static (List<double> Values,List<Signal> Signals) Calculate(StockData data,int minimum,int maximum,int cutoff)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new DiscreteFourierCycle(minimum,maximum,cutoff);var output=new List<double>(input.Count);var signals=new List<Signal>(input.Count);foreach(var price in input){output.Add(state.Next(price,true,out _));signals.Add(state.LastSignal);}return(output,signals);
    }
}
