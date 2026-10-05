using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using F=OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class DftSpectrumWindow : IDisposable
{
    private readonly int _upper,_lower;private readonly EhlersRoofingFilterV2Kernel _roof;private readonly Queue<BigInteger> _history=new();private Dictionary<int,F> _powers=new();private BigInteger _previous,_previousSlope;
    internal DftSpectrumWindow(int upper,int lower){_upper=Math.Max(1,upper);_lower=Math.Max(1,lower);_roof=new(_upper,_lower);}
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    internal static BigInteger RoundPower(BigInteger numerator,BigInteger denominator)
    {
        // Start below the first potentially finite exponent, then certify normally.
        var excess=(numerator.ToByteArray().Length-denominator.ToByteArray().Length)*8-2106;
        for(var shift=Math.Max(0,excess/32*32);;shift+=32){var value=ExactMeanAccumulator.UnitRatio(numerator,denominator<<shift);if(!double.IsInfinity(value))return U(value)<<shift;}
    }
    internal (double Value,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));_roof.Next(price,false,true);var roof=_roof.ExactOutput;var current=U(roof.Mantissa)<<roof.UpperShift;
        var kept=(long)_history.Count==1L+_upper?_history.Skip(1).ToArray():_history.ToArray();var samples=new BigInteger[kept.Length+1];samples[0]=current;for(var i=0;i<kept.Length;i++)samples[i+1]=kept[kept.Length-1-i];
        var powers=new Dictionary<int,F>();F peak=0;
        for(long period=_lower;period<=_upper;period++)
        {
            BigInteger real=0,imaginary=0;for(var lag=0;lag<samples.Length;lag++){var angle=2*Math.PI*((double)lag/period);real+=samples[lag]*U(Math.Cos(angle));imaginary+=samples[lag]*U(Math.Sin(angle));}
            var energy=real*real+imaginary*imaginary;var previous=_powers.TryGetValue((int)period,out var old)?old:default;var power=F.Of(.2)*new F(energy*energy,BigInteger.One)+F.Of(.8)*previous;power=new F(RoundPower(power.Numerator,power.Denominator),BigInteger.One);powers.Add((int)period,power);if(power>peak)peak=power;
        }
        F weighted=0,total=0;foreach(var bin in powers)if(peak.Sign>0&&2*bin.Value>=peak){weighted+=bin.Key*bin.Value;total+=bin.Value;}
        var value=total.Sign==0?0:(weighted/total).Publish();var slope=current-_previous;var trade=slope.Sign>0&&slope>_previousSlope?Signal.StrongBuy:slope.Sign<0&&slope<_previousSlope?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None;
        if(final){_roof.Next(price,true);if((long)_history.Count==1L+_upper)_history.Dequeue();_history.Enqueue(current);_powers=powers;_previous=current;_previousSlope=slope;}return(value,trade);
    }
    internal void Reset(){_roof.Reset();_history.Clear();_powers.Clear();_previous=_previousSlope=default;}
    public void Dispose()=>Reset();
    internal static (List<double> Values,List<Signal> Signals) Calculate(StockData data,int upper,int lower)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new DftSpectrumWindow(upper,lower);var output=new List<double>(input.Count);var signals=new List<Signal>(input.Count);foreach(var price in input){var point=state.Next(price,true);output.Add(point.Value);signals.Add(point.Trade);}return(output,signals);
    }
}
