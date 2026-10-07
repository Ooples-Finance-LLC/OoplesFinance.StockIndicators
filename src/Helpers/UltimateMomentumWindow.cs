using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using N=OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class UltimateMomentumWindow : IDisposable
{
    private sealed class Flow
    {
        private readonly int _length;private readonly Queue<(BigInteger Positive,BigInteger Negative)> _history=new();private BigInteger _positive,_negative;private double _previous;private bool _hasPrevious;
        internal Flow(int length)=>_length=Math.Max(1,length);
        internal double Next(double price,double volume,bool final)
        {
            var product=U(price)*U(volume);var positive=_hasPrevious&&price>_previous?product:BigInteger.Zero;var negative=_hasPrevious&&price<_previous?product:BigInteger.Zero;var old=_history.Count==_length?_history.Peek():default;var p=_positive+positive-old.Positive;var n=_negative+negative-old.Negative;var total=p+n;
            var value=n.IsZero?100:p.IsZero?0:total.IsZero?p.Sign>0?100:0:Math.Max(0,Math.Min(100,ExactMeanAccumulator.UnitRatio((100*p*total.Sign)<<1074,BigInteger.Abs(total))));
            if(final){if(_history.Count==_length)_history.Dequeue();_history.Enqueue((positive,negative));_positive=p;_negative=n;_previous=price;_hasPrevious=true;}return value;
        }
        internal void Reset(){_history.Clear();_positive=_negative=default;_previous=0;_hasPrevious=false;}
    }
    private readonly MovingAvgType _kind;private readonly int _strengthLength,_fastLength,_bandLength;private readonly double _multiplier;
    private readonly RocBankAverage _fast,_slow,_center,_gain,_loss,_signal;private readonly Flow _flow1,_flow2,_flow3;private readonly Queue<int> _directions=new();private readonly Queue<BigInteger> _prices=new();
    private int _advances,_declines;private BigInteger _sum,_squares,_previousPrice,_blend,_previousOutput,_previousSlope;private double _strength;private bool _hasBlend;
    internal static bool Supports(MovingAvgType kind)=>StrengthWindow.Supports(kind);
    internal UltimateMomentumWindow(MovingAvgType kind,int strength,int fast,int middle,int slow,int band,double multiplier)
    {
        StreamingInputValidation.Finite(multiplier,nameof(multiplier));_kind=kind;_strengthLength=Math.Max(1,strength);_fastLength=Math.Max(1,fast);_bandLength=Math.Max(1,band);_multiplier=multiplier;
        _fast=new(kind,fast,1,true);_slow=new(kind,slow,1,true);_center=new(kind,band,1,true);_gain=new(kind,strength,1,true);_loss=new(kind,strength,1,true);_signal=new(kind,strength,1,true);_flow1=new(fast);_flow2=new(middle);_flow3=new(slow);
    }
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    private static BigInteger U(RocBankValue value)=>U(value.Mantissa)<<value.UpperShift;
    private static BigInteger Round(N value)=>RocBankValue.RoundUnits(value.Numerator,value.Denominator);
    private static RocBankValue Value(BigInteger units){for(var shift=0;;shift+=32){var value=ExactMeanAccumulator.UnitRatio(units,BigInteger.One<<shift);if(!double.IsInfinity(value))return new(value,shift);}}
    private static BigInteger Position(N numerator,N variance)
    {
        var center=N.Integer(U(50));if(numerator.Sign==0)return Round(center);var square=(numerator*numerator).Divide(variance);
        for(var precision=64;;precision=checked(precision*2)){var scaled=square.Numerator<<(2*precision);var floor=ExactPopulationDeviation.IntegerRoot(scaled/square.Denominator);var ceiling=floor*floor*square.Denominator==scaled?floor:floor+1;var grid=N.Integer(BigInteger.One<<precision);var low=Round(center+N.Integer(numerator.Sign>0?floor:-ceiling).Divide(grid));var high=Round(center+N.Integer(numerator.Sign>0?ceiling:-floor).Divide(grid));if(low==high)return low;}
    }
    internal (double Value,Signal Trade) Next(double price,double typical,double volume,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));StreamingInputValidation.Finite(typical,nameof(typical));StreamingInputValidation.Finite(volume,nameof(volume));var current=U(price);var direction=current.CompareTo(_previousPrice);var removed=_directions.Count==_fastLength?_directions.Peek():0;var advances=_advances+(direction>0?1:0)-(removed>0?1:0);var declines=_declines+(direction<0?1:0)-(removed<0?1:0);
        var net=advances+declines==0?0:ExactMeanAccumulator.UnitRatio((new BigInteger(1000)*(advances-declines))<<1074,advances+declines);var mo=U(_fast.Next(new(net),final))-U(_slow.Next(new(net),final));var center=U(_center.Next(new(price),final));
        var expired=_prices.Count==_bandLength?_prices.Peek():BigInteger.Zero;var sum=_sum+current-expired;var squares=_squares+current*current-expired*expired;var count=Math.Min(_bandLength,_prices.Count+1L);var variance=N.Integer(count*squares-sum*sum).Divide(count*count);BigInteger position=0;
        if(count==_bandLength&&variance.Sign>0&&_multiplier!=0)position=Position(N.Integer(U(50)*(current-center)).Divide(N.Of(_multiplier)),variance);
        var f1=_flow1.Next(typical,volume,final);var f2=_flow2.Next(typical,volume,final);var f3=_flow3.Next(typical,volume,final);var ratio=declines==0?default:N.Integer(U(100)*advances).Divide(declines);
        var blend=Round(N.Integer(200*position+2*mo+3*U(f1)+3*U(f2))+N.Integer(U(f3))*N.Of(1.5)+ratio);
        if(_hasBlend&&(N.Integer(BigInteger.Abs(blend-_blend))-N.Integer(BigInteger.Max(BigInteger.Abs(blend),BigInteger.Abs(_blend)))*N.Of(1.4210854715202004e-14)).Sign<=0)blend=_blend;
        var change=_hasBlend?blend-_blend:BigInteger.Zero;var gain=U(_gain.Next(Value(BigInteger.Max(0,change)),final));var loss=U(_loss.Next(Value(BigInteger.Max(0,-change)),final));var strength=loss.IsZero?100:gain.IsZero?0:ExactMeanAccumulator.UnitRatio((100*gain)<<1074,gain+loss);
        if(_strengthLength>1&&(_kind is MovingAvgType.ExponentialMovingAverage or MovingAvgType.WildersSmoothingMethod)&&_hasBlend&&change.IsZero)strength=_strength;
        var output=U(_signal.Next(new(strength),final));var slope=output-_previousOutput;var trade=slope.Sign>0&&slope>_previousSlope?Signal.StrongBuy:slope.Sign<0&&slope<_previousSlope?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None;
        if(final){if(_directions.Count==_fastLength)_directions.Dequeue();_directions.Enqueue(direction);_advances=advances;_declines=declines;if(_prices.Count==_bandLength)_prices.Dequeue();_prices.Enqueue(current);_sum=sum;_squares=squares;_previousPrice=current;_blend=blend;_strength=strength;_hasBlend=true;_previousOutput=output;_previousSlope=slope;}return(ExactMeanAccumulator.UnitRatio(output,BigInteger.One),trade);
    }
    internal void Reset(){foreach(var average in new[]{_fast,_slow,_center,_gain,_loss,_signal})average.Reset();_flow1.Reset();_flow2.Reset();_flow3.Reset();_directions.Clear();_prices.Clear();_advances=_declines=0;_sum=_squares=_previousPrice=_blend=_previousOutput=_previousSlope=default;_strength=0;_hasBlend=false;}
    public void Dispose(){foreach(var average in new[]{_fast,_slow,_center,_gain,_loss,_signal})average.Dispose();}
    internal static (List<double> Values,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int strength,int fast,int middle,int slow,int band,double multiplier)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new UltimateMomentumWindow(kind,strength,fast,middle,slow,band,multiplier);var output=new List<double>(input.Count);var signals=new List<Signal>(input.Count);
        for(var i=0;i<input.Count;i++){var typical=data.ChainedValues.Count>0?input[i]:RollingMoneyFlowIndex.TypicalPrice(data.HighPrices[i],data.LowPrices[i],data.ClosePrices[i]);var p=state.Next(input[i],typical,data.Volumes[i],true);output.Add(p.Value);signals.Add(p.Trade);}return(output,signals);
    }
}
